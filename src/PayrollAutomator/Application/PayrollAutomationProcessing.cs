namespace PayrollAutomator.Application;

public sealed record PayrollWorkbookCandidate(
    string SourcePath,
    string OriginalFileName,
    long Size,
    DateTimeOffset LastWriteTimeUtc,
    bool IsStable,
    string? ContentHash,
    string? FailureCode);

public sealed record PayrollAutomationProcessResult(
    Guid? JobId,
    PayrollAutomationResultCode Code,
    PayrollAutomationJobStatus Status,
    Guid? PayrollRunId = null,
    string? FailureCode = null);

public sealed record PayrollAutomationScanSummary(
    int CandidateCount,
    int ProcessedCount,
    int NeedsReviewCount,
    int DuplicateCount,
    int WaitingForStabilityCount,
    int QuarantinedCount,
    int FailedCount)
{
    public bool HasFailures => FailedCount > 0;
}

public interface IWorkbookDiscovery
{
    Task<IReadOnlyList<PayrollWorkbookCandidate>> DiscoverAsync(
        string inputDirectory,
        long maximumFileSize,
        TimeSpan stabilityDelay,
        CancellationToken cancellationToken = default);

    Task<PayrollWorkbookCandidate> InspectAsync(
        string sourcePath,
        long maximumFileSize,
        TimeSpan stabilityDelay,
        CancellationToken cancellationToken = default);
}

public sealed class PayrollAutomationProcessor(
    PayrollStorageOptions options,
    IAutomationJobRepository jobs,
    PayrollRunService runs,
    IWorkbookDiscovery discovery)
{
    public async Task<PayrollAutomationProcessResult> ProcessFileAsync(
        string sourcePath,
        string trigger = "Manual",
        bool quarantineInvalidFile = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(trigger);

        PayrollWorkbookCandidate candidate = await discovery.InspectAsync(
            Path.GetFullPath(sourcePath),
            options.MaxUploadBytes,
            options.Automation.FileStabilityDelay,
            cancellationToken);
        return await ProcessCandidateAsync(candidate, trigger, quarantineInvalidFile, cancellationToken);
    }

    public async Task<PayrollAutomationScanSummary> ScanOnceAsync(
        string inputDirectory,
        string trigger = "ScheduledScan",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(trigger);
        IReadOnlyList<PayrollWorkbookCandidate> candidates = await discovery.DiscoverAsync(
            Path.GetFullPath(inputDirectory),
            options.MaxUploadBytes,
            options.Automation.FileStabilityDelay,
            cancellationToken);

        int processed = 0;
        int needsReview = 0;
        int duplicates = 0;
        int waiting = 0;
        int quarantined = 0;
        int failed = 0;
        foreach (PayrollWorkbookCandidate candidate in candidates)
        {
            PayrollAutomationProcessResult result = await ProcessCandidateAsync(
                candidate,
                trigger,
                quarantineInvalidFile: true,
                cancellationToken);
            switch (result.Code)
            {
                case PayrollAutomationResultCode.Completed:
                    processed++;
                    break;
                case PayrollAutomationResultCode.NeedsReview:
                    processed++;
                    needsReview++;
                    break;
                case PayrollAutomationResultCode.WaitingForStability:
                    waiting++;
                    break;
                case PayrollAutomationResultCode.SkippedDuplicate:
                    duplicates++;
                    break;
                case PayrollAutomationResultCode.Quarantined:
                    quarantined++;
                    break;
                case PayrollAutomationResultCode.Failed:
                    failed++;
                    break;
            }

        }

        return new PayrollAutomationScanSummary(
            candidates.Count, processed, needsReview, duplicates, waiting, quarantined, failed);
    }

    private async Task<PayrollAutomationProcessResult> ProcessCandidateAsync(
        PayrollWorkbookCandidate candidate,
        string trigger,
        bool quarantineInvalidFile,
        CancellationToken cancellationToken)
    {
        string identityHash = candidate.ContentHash ?? CreateMetadataHash(candidate);
        PayrollAutomationJobRecord? existing = await jobs.FindBySourceAsync(
            candidate.SourcePath,
            identityHash,
            cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid jobId;
        if (existing is not null)
        {
            if (existing.Status == PayrollAutomationJobStatus.Failed &&
                candidate.FailureCode is null &&
                candidate.IsStable)
            {
                bool requeued = await jobs.TryRequeueFailedAsync(
                    existing.Id,
                    now,
                    options.Automation.MaxAttempts,
                    cancellationToken);
                if (requeued)
                {
                    jobId = existing.Id;
                }
                else
                {
                    return new PayrollAutomationProcessResult(
                        existing.Id,
                        PayrollAutomationResultCode.Failed,
                        PayrollAutomationJobStatus.Failed,
                        FailureCode: existing.FailureCode ?? "MaxAttemptsExceeded");
                }
            }
            else
            {
                return new PayrollAutomationProcessResult(
                    existing.Id,
                    PayrollAutomationResultCode.SkippedDuplicate,
                    PayrollAutomationJobStatus.SkippedDuplicate,
                    existing.PayrollRunId);
            }
        }
        else
        {
            jobId = Guid.NewGuid();
            var job = new PayrollAutomationJobRecord
            {
                Id = jobId,
                SourcePath = candidate.SourcePath,
                OriginalFileName = candidate.OriginalFileName,
                ContentHash = identityHash,
                Trigger = trigger,
                Status = candidate.FailureCode is null && candidate.IsStable
                    ? PayrollAutomationJobStatus.Queued
                    : candidate.FailureCode is null
                        ? PayrollAutomationJobStatus.WaitingForStableFile
                        : PayrollAutomationJobStatus.Quarantined,
                ResultCode = candidate.FailureCode is null ? null : PayrollAutomationResultCode.Quarantined,
                DiscoveredAtUtc = now,
                UpdatedAtUtc = now,
                FailureCode = candidate.FailureCode
            };
            await jobs.AddAsync(job, cancellationToken);

            if (candidate.FailureCode is not null)
            {
                if (quarantineInvalidFile)
                {
                    MoveToQuarantine(candidate.SourcePath);
                }

                return new PayrollAutomationProcessResult(
                    jobId,
                    PayrollAutomationResultCode.Quarantined,
                    PayrollAutomationJobStatus.Quarantined,
                    FailureCode: candidate.FailureCode);
            }

            if (!candidate.IsStable)
            {
                return new PayrollAutomationProcessResult(
                    jobId,
                    PayrollAutomationResultCode.WaitingForStability,
                    PayrollAutomationJobStatus.WaitingForStableFile,
                    FailureCode: "FileNotStable");
            }
        }

        string workerId = $"one-shot-{Environment.ProcessId}-{Guid.NewGuid():N}";
        bool claimed = await jobs.TryClaimAsync(
            jobId,
            workerId,
            now,
            TimeSpan.FromMinutes(10),
            options.Automation.MaxAttempts,
            cancellationToken);
        if (!claimed)
        {
            return new PayrollAutomationProcessResult(
                jobId,
                PayrollAutomationResultCode.Failed,
                PayrollAutomationJobStatus.Failed,
                FailureCode: "JobClaimFailed");
        }

        try
        {
            await using FileStream workbook = new(
                candidate.SourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            PayrollRunSummary run = await runs.CreateRunAsync(
                workbook,
                candidate.OriginalFileName,
                cancellationToken: cancellationToken);
            await jobs.TryUpdateOutcomeAsync(
                jobId,
                workerId,
                PayrollAutomationJobStatus.NeedsReview,
                PayrollAutomationResultCode.NeedsReview,
                run.Id,
                DateTimeOffset.UtcNow,
                cancellationToken: cancellationToken);
            return new PayrollAutomationProcessResult(
                jobId,
                PayrollAutomationResultCode.NeedsReview,
                PayrollAutomationJobStatus.NeedsReview,
                run.Id);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or IOException)
        {
            await jobs.TryUpdateOutcomeAsync(
                jobId,
                workerId,
                PayrollAutomationJobStatus.Failed,
                PayrollAutomationResultCode.Failed,
                null,
                DateTimeOffset.UtcNow,
                "ProcessingFailed",
                exception.GetType().Name,
                cancellationToken);
            return new PayrollAutomationProcessResult(
                jobId,
                PayrollAutomationResultCode.Failed,
                PayrollAutomationJobStatus.Failed,
                FailureCode: "ProcessingFailed");
        }
    }

    private void MoveToQuarantine(string sourcePath)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        string quarantineDirectory = PayrollAutomationConfiguration.ResolvePath(
            options,
            options.Paths.Quarantine);
        Directory.CreateDirectory(quarantineDirectory);
        string fileName = Path.GetFileName(sourcePath);
        string destination = Path.Combine(quarantineDirectory, fileName);
        if (File.Exists(destination))
        {
            destination = Path.Combine(
                quarantineDirectory,
                $"{Path.GetFileNameWithoutExtension(fileName)}.{Guid.NewGuid():N}{Path.GetExtension(fileName)}");
        }

        File.Move(sourcePath, destination);
    }

    private static string CreateMetadataHash(PayrollWorkbookCandidate candidate)
    {
        string identity = string.Join(
            "|",
            candidate.SourcePath,
            candidate.Size,
            candidate.LastWriteTimeUtc.Ticks,
            candidate.FailureCode);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(identity)));
    }
}
