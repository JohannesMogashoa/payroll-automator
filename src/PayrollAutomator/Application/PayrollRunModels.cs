using PayrollAutomator.Domain;

namespace PayrollAutomator.Application;

public enum PayrollRunStatus
{
    NeedsReview,
    Confirmed,
    Exported,
    Failed,
    Deleted
}

public sealed record PayrollRunSummary(
    Guid Id,
    string OriginalFileName,
    PayrollRunStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int ErrorCount,
    int WarningCount,
    decimal IncludedPayrollHours,
    decimal IncludedPayableAmount,
    bool HasExport);

public sealed record PayrollRunDetails(
    PayrollRunSummary Summary,
    PayrollCalculationResult Calculation,
    PayrollReviewSummary Review,
    IReadOnlyList<AuditLogEntry> AuditEntries,
    string? Reviewer,
    DateTimeOffset? ConfirmedAtUtc,
    DateTimeOffset? ExportedAtUtc);

public sealed record PayrollDownload(
    Stream Content,
    string FileName);

public sealed record PayrollStorageOptions
{
    public string DataRoot { get; init; } = Path.Combine(AppContext.BaseDirectory, "data");

    public long MaxUploadBytes { get; init; } = 25 * 1024 * 1024;

    public int RetentionDays { get; init; } = 90;

    public PayrollAutomationOptions Automation { get; init; } = new();

    public PayrollAutomationPathOptions Paths { get; init; } = new();
}

public interface IPayrollFileStore
{
    Task<string> SaveSourceAsync(
        Guid runId,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken = default);

    string GetAbsolutePath(string relativePath);

    string GetExportRelativePath(Guid runId);

    Task DeleteRunArtifactsAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<PayrollDownload?> OpenExportAsync(
        Guid runId,
        string fileName,
        CancellationToken cancellationToken = default);
}

public interface IPayrollRunService
{
    Task<PayrollRunSummary> CreateRunAsync(
        Stream workbook,
        string originalFileName,
        PayrollRatePolicy? ratePolicy = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PayrollRunSummary>> ListRunsAsync(
        CancellationToken cancellationToken = default);

    Task<PayrollRunDetails?> GetRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<PayrollExportResult> ConfirmAndExportAsync(
        Guid runId,
        string reviewer,
        string? comment = null,
        CancellationToken cancellationToken = default);

    Task<PayrollDownload?> OpenExportAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task DeleteRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default,
        string auditEventType = "RunDeleted",
        string auditDetails = "Run artifacts and calculation snapshot were deleted by the operator.");
}
