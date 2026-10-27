namespace PayrollAutomator.Application;

public interface IAutomationJobRepository
{
    Task AddAsync(
        PayrollAutomationJobRecord job,
        CancellationToken cancellationToken = default);

    Task<PayrollAutomationJobRecord?> GetAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);

    Task<PayrollAutomationJobRecord?> FindBySourceAsync(
        string sourcePath,
        string contentHash,
        CancellationToken cancellationToken = default);

    Task<bool> TryClaimAsync(
        Guid jobId,
        string workerId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        int maxAttempts,
        CancellationToken cancellationToken = default);

    Task<bool> TryUpdateOutcomeAsync(
        Guid jobId,
        string workerId,
        PayrollAutomationJobStatus status,
        PayrollAutomationResultCode? resultCode,
        Guid? payrollRunId,
        DateTimeOffset nowUtc,
        string? failureCode = null,
        string? failureDetails = null,
        CancellationToken cancellationToken = default);
}
