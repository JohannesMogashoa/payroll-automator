namespace PayrollAutomator.Application;

public interface IWorkbookImporter
{
    WorkbookImportResult Import(string sourcePath);
}

public sealed record WorkbookImportResult(
    IReadOnlyList<ImportedPayrollRow> Rows,
    IReadOnlyList<WorkbookImportIssue> Issues)
{
    public bool HasErrors => Issues.Any(issue => issue.Severity == ImportIssueSeverity.Error);
}

public sealed record ImportedPayrollRow(
    string SheetName,
    int SourceRowNumber,
    string PeriodLabel,
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    string EmployeeNumber,
    string EmployeeName,
    PayrollCellValue Normal,
    PayrollCellValue Overtime,
    PayrollCellValue Sunday,
    PayrollCellValue PublicHoliday,
    PayrollCellValue NightShift,
    PayrollCellValue SickLeave,
    string? Comment);

public sealed record PayrollCellValue(decimal? RawValue, string? NumberFormat)
{
    public bool HasValue => RawValue.HasValue;
}

public enum ImportIssueSeverity
{
    Warning,
    Error
}

public sealed record WorkbookImportIssue(
    ImportIssueSeverity Severity,
    string Code,
    string Message,
    string SheetName,
    int? SourceRowNumber = null,
    string? CellAddress = null);
