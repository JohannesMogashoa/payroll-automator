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

public static class PayrollAutomationConfiguration
{
    private static readonly string[] PathPropertyNames =
    [
        nameof(PayrollAutomationPathOptions.Root),
        nameof(PayrollAutomationPathOptions.Inbox),
        nameof(PayrollAutomationPathOptions.Processing),
        nameof(PayrollAutomationPathOptions.Processed),
        nameof(PayrollAutomationPathOptions.Quarantine),
        nameof(PayrollAutomationPathOptions.Output),
        nameof(PayrollAutomationPathOptions.Backup)
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

        foreach (string propertyName in PathPropertyNames)
        {
            string? value = typeof(PayrollAutomationPathOptions)
                .GetProperty(propertyName)?
                .GetValue(paths) as string;
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
