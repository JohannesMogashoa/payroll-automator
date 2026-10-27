namespace PayrollAutomator.Application;

public sealed record PayrollAutomationStatusSummary(
    bool Enabled,
    PayrollAutomationMode Mode,
    int TotalJobs,
    int QueuedJobs,
    int ProcessingJobs,
    int NeedsReviewJobs,
    int FailedJobs,
    int QuarantinedJobs,
    DateTimeOffset? LastJobUpdatedAtUtc);

public sealed record PayrollRetentionPreview(
    DateTimeOffset CutoffUtc,
    int EligibleRunCount,
    int EligibleJobCount,
    int ProtectedRunCount,
    int ProtectedJobCount);

public sealed class PayrollAutomationOperationsService(
    PayrollStorageOptions options,
    IAutomationJobRepository jobs,
    IPayrollRunRepository runs,
    PayrollAutomationProcessor processor)
{
    public async Task<PayrollAutomationStatusSummary> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PayrollAutomationJobRecord> records = await jobs.ListAsync(cancellationToken);
        return new PayrollAutomationStatusSummary(
            options.Automation.Enabled,
            options.Automation.Mode,
            records.Count,
            records.Count(job => job.Status == PayrollAutomationJobStatus.Queued),
            records.Count(job => job.Status == PayrollAutomationJobStatus.Processing),
            records.Count(job => job.Status == PayrollAutomationJobStatus.NeedsReview),
            records.Count(job => job.Status == PayrollAutomationJobStatus.Failed),
            records.Count(job => job.Status == PayrollAutomationJobStatus.Quarantined),
            records.Count == 0 ? null : records.Max(job => job.UpdatedAtUtc));
    }

    public Task<PayrollAutomationScanSummary> ScanNowAsync(
        CancellationToken cancellationToken = default)
    {
        string inbox = PayrollAutomationConfiguration.ResolvePath(options, options.Paths.Inbox);
        return processor.ScanOnceAsync(inbox, "WebScanNow", cancellationToken);
    }

    public async Task<PayrollRetentionPreview> PreviewRetentionAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset cutoffUtc = nowUtc.AddDays(-options.RetentionDays);
        IReadOnlyList<PayrollRunRecord> runRecords = await runs.ListAsync(cancellationToken);
        IReadOnlyList<PayrollAutomationJobRecord> jobRecords = await jobs.ListAsync(cancellationToken);

        int eligibleRuns = runRecords.Count(run =>
            run.UpdatedAtUtc <= cutoffUtc &&
            run.Status is not PayrollRunStatus.NeedsReview and not PayrollRunStatus.Deleted);
        int protectedRuns = runRecords.Count - eligibleRuns;
        int eligibleJobs = jobRecords.Count(job =>
            job.UpdatedAtUtc <= cutoffUtc &&
            job.Status is PayrollAutomationJobStatus.Exported or
                PayrollAutomationJobStatus.SkippedDuplicate or
                PayrollAutomationJobStatus.Quarantined or
                PayrollAutomationJobStatus.Failed);
        int protectedJobs = jobRecords.Count - eligibleJobs;

        return new PayrollRetentionPreview(
            cutoffUtc,
            eligibleRuns,
            eligibleJobs,
            protectedRuns,
            protectedJobs);
    }
}
