using PayrollAutomator.Domain;

namespace PayrollAutomator.Application;

public enum PayrollReviewSeverity
{
    Warning,
    Error
}

public sealed record PayrollReviewException(
    PayrollReviewSeverity Severity,
    string Code,
    string Message,
    string SheetName,
    int? SourceRowNumber,
    string? CellAddress,
    PayrollCategory? Category);

public sealed record PayrollReviewSummary(
    IReadOnlyList<PayrollReviewException> Exceptions,
    int CalculatedRowCount,
    int CompleteRowCount,
    int IncompleteRowCount,
    int ProrataRowCount,
    decimal IncludedPayrollHours,
    decimal IncludedPayableAmount)
{
    public int ErrorCount => Exceptions.Count(exception => exception.Severity == PayrollReviewSeverity.Error);

    public int WarningCount => Exceptions.Count(exception => exception.Severity == PayrollReviewSeverity.Warning);

    public bool HasErrors => ErrorCount > 0;
}

public sealed record ReviewConfirmation(
    string Reviewer,
    DateTimeOffset ConfirmedAtUtc,
    string? Comment,
    int AcknowledgedWarningCount);

public sealed record PayrollExportRequest(
    string SourcePath,
    string OutputPath,
    PayrollCalculationResult Calculation,
    PayrollReviewSummary Review,
    ReviewConfirmation Confirmation);

public sealed record AuditLogEntry(
    DateTimeOffset TimestampUtc,
    string EventType,
    string Details);

public sealed record PayrollExportResult(
    string OutputPath,
    IReadOnlyList<AuditLogEntry> AuditEntries);
