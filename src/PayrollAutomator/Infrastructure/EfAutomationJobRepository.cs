using Microsoft.EntityFrameworkCore;
using PayrollAutomator.Application;

namespace PayrollAutomator.Infrastructure;

public sealed class EfAutomationJobRepository(PayrollDbContext db) : IAutomationJobRepository
{
    public async Task AddAsync(
        PayrollAutomationJobRecord job,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        db.AutomationJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<PayrollAutomationJobRecord?> GetAsync(
        Guid jobId,
        CancellationToken cancellationToken = default) =>
        db.AutomationJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(job => job.Id == jobId, cancellationToken);

    public Task<PayrollAutomationJobRecord?> FindBySourceAsync(
        string sourcePath,
        string contentHash,
        CancellationToken cancellationToken = default) =>
        db.AutomationJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(
                job => job.SourcePath == sourcePath && job.ContentHash == contentHash,
                cancellationToken);

    public async Task<IReadOnlyList<PayrollAutomationJobRecord>> ListAsync(
        CancellationToken cancellationToken = default) =>
        (await db.AutomationJobs
            .AsNoTracking()
            .ToListAsync(cancellationToken))
        .OrderByDescending(job => job.UpdatedAtUtc)
        .ToArray();

    public async Task<bool> TryClaimAsync(
        Guid jobId,
        string workerId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        int maxAttempts,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        }

        string queuedStatus = nameof(PayrollAutomationJobStatus.Queued);
        string processingStatus = nameof(PayrollAutomationJobStatus.Processing);
        int updated = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "AutomationJobs"
            SET "Status" = {processingStatus},
                "AttemptCount" = "AttemptCount" + 1,
                "WorkerId" = {workerId},
                "LeaseExpiresAtUtc" = {nowUtc.Add(leaseDuration)},
                "UpdatedAtUtc" = {nowUtc}
            WHERE "Id" = {jobId}
              AND "AttemptCount" < {maxAttempts}
              AND (
                    "Status" = {queuedStatus}
                    OR (
                        "Status" = {processingStatus}
                        AND "LeaseExpiresAtUtc" IS NOT NULL
                        AND "LeaseExpiresAtUtc" <= {nowUtc}
                    )
                  )
            """, cancellationToken);

        return updated == 1;
    }

    public async Task<bool> TryRequeueFailedAsync(
        Guid jobId,
        DateTimeOffset nowUtc,
        int maxAttempts,
        CancellationToken cancellationToken = default)
    {
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        }

        string failedStatus = nameof(PayrollAutomationJobStatus.Failed);
        string queuedStatus = nameof(PayrollAutomationJobStatus.Queued);
        int updated = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "AutomationJobs"
            SET "Status" = {queuedStatus},
                "ResultCode" = NULL,
                "FailureCode" = NULL,
                "FailureDetails" = NULL,
                "UpdatedAtUtc" = {nowUtc}
            WHERE "Id" = {jobId}
              AND "Status" = {failedStatus}
              AND "AttemptCount" < {maxAttempts}
            """, cancellationToken);

        return updated == 1;
    }

    public async Task<bool> TryUpdateOutcomeAsync(
        Guid jobId,
        string workerId,
        PayrollAutomationJobStatus status,
        PayrollAutomationResultCode? resultCode,
        Guid? payrollRunId,
        DateTimeOffset nowUtc,
        string? failureCode = null,
        string? failureDetails = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        if (status is PayrollAutomationJobStatus.Discovered or
            PayrollAutomationJobStatus.WaitingForStableFile or
            PayrollAutomationJobStatus.Queued or
            PayrollAutomationJobStatus.Processing)
        {
            throw new ArgumentException(
                "Outcome updates must use a completed or retryable job status.",
                nameof(status));
        }

        if (status == PayrollAutomationJobStatus.Failed && string.IsNullOrWhiteSpace(failureCode))
        {
            throw new ArgumentException(
                "A failure code is required when marking an automation job as failed.",
                nameof(failureCode));
        }

        string processingStatus = nameof(PayrollAutomationJobStatus.Processing);
        string outcomeStatus = status.ToString();
        string? outcomeCode = resultCode?.ToString();
        int updated = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "AutomationJobs"
            SET "Status" = {outcomeStatus},
                "ResultCode" = {outcomeCode},
                "PayrollRunId" = {payrollRunId},
                "FailureCode" = {failureCode},
                "FailureDetails" = {failureDetails},
                "WorkerId" = NULL,
                "LeaseExpiresAtUtc" = NULL,
                "UpdatedAtUtc" = {nowUtc}
            WHERE "Id" = {jobId}
              AND "Status" = {processingStatus}
              AND "WorkerId" = {workerId}
            """, cancellationToken);

        return updated == 1;
    }
}
