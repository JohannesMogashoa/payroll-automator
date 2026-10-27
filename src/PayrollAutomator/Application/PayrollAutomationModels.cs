namespace PayrollAutomator.Application;

public enum PayrollAutomationMode
{
    ScheduledScan
}

public enum PayrollAutomationJobStatus
{
    Discovered,
    WaitingForStableFile,
    Queued,
    Processing,
    NeedsReview,
    Exported,
    SkippedDuplicate,
    Quarantined,
    Failed
}

public enum PayrollAutomationResultCode
{
    Completed,
    NeedsReview,
    WaitingForStability,
    SkippedDuplicate,
    Quarantined,
    Failed,
    InvalidConfiguration
}

public static class PayrollAutomationAuditEvents
{
    public const string JobDiscovered = "AutomationJobDiscovered";
    public const string JobClaimed = "AutomationJobClaimed";
    public const string JobCompleted = "AutomationJobCompleted";
    public const string JobNeedsReview = "AutomationJobNeedsReview";
    public const string JobSkippedDuplicate = "AutomationJobSkippedDuplicate";
    public const string JobQuarantined = "AutomationJobQuarantined";
    public const string JobFailed = "AutomationJobFailed";
    public const string CleanupStarted = "AutomationCleanupStarted";
    public const string CleanupCompleted = "AutomationCleanupCompleted";
    public const string BackupCompleted = "AutomationBackupCompleted";
    public const string BackupFailed = "AutomationBackupFailed";
}

public sealed record PayrollAutomationOptions
{
    public bool Enabled { get; init; }

    public PayrollAutomationMode Mode { get; init; } = PayrollAutomationMode.ScheduledScan;

    public TimeSpan ScanInterval { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan FileStabilityDelay { get; init; } = TimeSpan.FromSeconds(30);

    public int MaxConcurrentJobs { get; init; } = 1;

    public int MaxAttempts { get; init; } = 3;

    public bool AutoExportEnabled { get; init; }

    public bool AutoExportRequireNoWarnings { get; init; } = true;

    public string? ServiceReviewer { get; init; }

    public string NotifyOn { get; init; } = "Completed,NeedsReview,Failed";

    public string NotificationProvider { get; init; } = "Console";

    public string? NotificationEndpoint { get; init; }
}

public sealed record PayrollAutomationPathOptions
{
    public string Root { get; init; } = "automation";

    public string Inbox { get; init; } = "inbox";

    public string Processing { get; init; } = "processing";

    public string Processed { get; init; } = "processed";

    public string Quarantine { get; init; } = "quarantine";

    public string Output { get; init; } = "output";

    public string Backup { get; init; } = "backups";
}

public sealed record PayrollAutomationJobResult(
    PayrollAutomationResultCode Code,
    PayrollAutomationJobStatus Status,
    string? FailureCode = null);

public sealed class PayrollAutomationJobRecord
{
    public Guid Id { get; set; }

    public string SourcePath { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentHash { get; set; } = string.Empty;

    public string Trigger { get; set; } = string.Empty;

    public PayrollAutomationJobStatus Status { get; set; }

    public PayrollAutomationResultCode? ResultCode { get; set; }

    public int AttemptCount { get; set; }

    public DateTimeOffset DiscoveredAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }

    public string? WorkerId { get; set; }

    public Guid? PayrollRunId { get; set; }

    public string? FailureCode { get; set; }

    public string? FailureDetails { get; set; }
}

public static class PayrollAutomationJobStateMachine
{
    public static bool CanTransition(
        PayrollAutomationJobStatus current,
        PayrollAutomationJobStatus next) =>
        (current, next) switch
        {
            (PayrollAutomationJobStatus.Discovered, PayrollAutomationJobStatus.WaitingForStableFile) => true,
            (PayrollAutomationJobStatus.Discovered, PayrollAutomationJobStatus.Queued) => true,
            (PayrollAutomationJobStatus.Discovered, PayrollAutomationJobStatus.Quarantined) => true,
            (PayrollAutomationJobStatus.Discovered, PayrollAutomationJobStatus.Failed) => true,
            (PayrollAutomationJobStatus.WaitingForStableFile, PayrollAutomationJobStatus.Queued) => true,
            (PayrollAutomationJobStatus.WaitingForStableFile, PayrollAutomationJobStatus.Quarantined) => true,
            (PayrollAutomationJobStatus.WaitingForStableFile, PayrollAutomationJobStatus.Failed) => true,
            (PayrollAutomationJobStatus.Queued, PayrollAutomationJobStatus.Processing) => true,
            (PayrollAutomationJobStatus.Queued, PayrollAutomationJobStatus.Failed) => true,
            (PayrollAutomationJobStatus.Processing, PayrollAutomationJobStatus.Queued) => true,
            (PayrollAutomationJobStatus.Processing, PayrollAutomationJobStatus.NeedsReview) => true,
            (PayrollAutomationJobStatus.Processing, PayrollAutomationJobStatus.Exported) => true,
            (PayrollAutomationJobStatus.Processing, PayrollAutomationJobStatus.SkippedDuplicate) => true,
            (PayrollAutomationJobStatus.Processing, PayrollAutomationJobStatus.Quarantined) => true,
            (PayrollAutomationJobStatus.Processing, PayrollAutomationJobStatus.Failed) => true,
            (PayrollAutomationJobStatus.NeedsReview, PayrollAutomationJobStatus.Exported) => true,
            _ => false
        };

    public static void EnsureTransition(
        PayrollAutomationJobStatus current,
        PayrollAutomationJobStatus next)
    {
        if (!CanTransition(current, next))
        {
            throw new InvalidOperationException(
                $"Automation job cannot transition from '{current}' to '{next}'.");
        }
    }
}

public static class PayrollAutomationConfiguration
{
    private static readonly (string Name, Func<PayrollAutomationPathOptions, string> GetValue)[] PathProperties =
    [
        (nameof(PayrollAutomationPathOptions.Root), paths => paths.Root),
        (nameof(PayrollAutomationPathOptions.Inbox), paths => paths.Inbox),
        (nameof(PayrollAutomationPathOptions.Processing), paths => paths.Processing),
        (nameof(PayrollAutomationPathOptions.Processed), paths => paths.Processed),
        (nameof(PayrollAutomationPathOptions.Quarantine), paths => paths.Quarantine),
        (nameof(PayrollAutomationPathOptions.Output), paths => paths.Output),
        (nameof(PayrollAutomationPathOptions.Backup), paths => paths.Backup)
    ];

    public static IReadOnlyList<string> Validate(PayrollStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();
        PayrollAutomationOptions automation = options.Automation;
        PayrollAutomationPathOptions paths = options.Paths;

        if (options.MaxUploadBytes <= 0)
        {
            errors.Add("Payroll:MaxUploadBytes must be greater than zero.");
        }

        if (options.RetentionDays < 0)
        {
            errors.Add("Payroll:RetentionDays cannot be negative.");
        }

        if (automation.ScanInterval <= TimeSpan.Zero)
        {
            errors.Add("Payroll:Automation:ScanInterval must be greater than zero.");
        }

        if (automation.FileStabilityDelay < TimeSpan.Zero)
        {
            errors.Add("Payroll:Automation:FileStabilityDelay cannot be negative.");
        }

        if (automation.MaxConcurrentJobs <= 0)
        {
            errors.Add("Payroll:Automation:MaxConcurrentJobs must be greater than zero.");
        }

        if (automation.MaxAttempts <= 0)
        {
            errors.Add("Payroll:Automation:MaxAttempts must be greater than zero.");
        }

        if (automation.AutoExportEnabled && string.IsNullOrWhiteSpace(automation.ServiceReviewer))
        {
            errors.Add("Payroll:Automation:ServiceReviewer is required when automatic export is enabled.");
        }

        foreach ((string propertyName, Func<PayrollAutomationPathOptions, string> getValue) in PathProperties)
        {
            string value = getValue(paths);
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"Payroll:Paths:{propertyName} cannot be empty.");
            }
            else if (Path.IsPathRooted(value) || value.Split(['/', '\\']).Contains("..", StringComparer.Ordinal))
            {
                errors.Add($"Payroll:Paths:{propertyName} must be a relative path within the configured automation root.");
            }
        }

        return errors;
    }

    public static string ResolvePath(PayrollStorageOptions options, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        IReadOnlyList<string> errors = Validate(options);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Invalid payroll automation configuration: {string.Join(" ", errors)}");
        }

        string automationRoot = Path.GetFullPath(Path.Combine(options.DataRoot, options.Paths.Root));
        string resolvedPath = Path.GetFullPath(Path.Combine(automationRoot, relativePath));
        if (!resolvedPath.StartsWith(automationRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(resolvedPath, automationRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The automation path escapes the configured automation root.");
        }

        return resolvedPath;
    }
}
