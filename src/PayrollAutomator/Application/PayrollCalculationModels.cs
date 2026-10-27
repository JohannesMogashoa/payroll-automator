using PayrollAutomator.Domain;

namespace PayrollAutomator.Application;

public enum CalculationIssueSeverity
{
    Warning,
    Error
}

public sealed record PayrollCalculationIssue(
    CalculationIssueSeverity Severity,
    string Code,
    string Message,
    string SheetName,
    int? SourceRowNumber = null,
    PayrollCategory? Category = null);

public sealed record PayrollCalculationMetadata(
    string RuleSetVersion,
    decimal HourlyRate,
    IReadOnlyDictionary<PayrollCategory, decimal> CategoryMultipliers);

public sealed record PayrollCalculationResult(
    IReadOnlyList<CalculatedPayrollRow> Rows,
    IReadOnlyList<EmployeePayrollTotal> EmployeeTotals,
    IReadOnlyList<WorkbookImportIssue> ImportIssues,
    IReadOnlyList<PayrollCalculationIssue> Issues,
    PayrollCalculationMetadata Metadata)
{
    public bool HasErrors =>
        ImportIssues.Any(issue => issue.Severity == ImportIssueSeverity.Error) ||
        Issues.Any(issue => issue.Severity == CalculationIssueSeverity.Error);
}
