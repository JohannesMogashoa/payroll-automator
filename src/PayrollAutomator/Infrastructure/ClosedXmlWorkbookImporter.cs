using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ClosedXML.Graphics;
using PayrollAutomator.Application;

namespace PayrollAutomator.Infrastructure;

public sealed class ClosedXmlWorkbookImporter : IWorkbookImporter
{
    private static readonly string[] RequiredHeaders =
    {
        "Date",
        "Employee Number",
        "Employee Name",
        "Normal",
        "Overtime 1.5",
        "Sunday 0.5",
        "PH",
        "Night Shift",
        "Sick Leave",
        "Comment"
    };

    private static readonly Regex PeriodLabelPattern = new(
        "^(?<start>\\d{4}-\\d{2}-\\d{2})\\s*-\\s*(?<end>\\d{4}-\\d{2}-\\d{2})$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public WorkbookImportResult Import(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var rows = new List<ImportedPayrollRow>();
        var issues = new List<WorkbookImportIssue>();

        using var stream = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        using Stream fallbackFont = OpenFallbackFont();
        var loadOptions = new LoadOptions
        {
            GraphicEngine = DefaultGraphicEngine.CreateOnlyWithFonts(fallbackFont)
        };
        using var workbook = new XLWorkbook(stream, loadOptions);

        foreach (IXLWorksheet worksheet in workbook.Worksheets)
        {
            ImportWorksheet(worksheet, rows, issues);
        }

        return new WorkbookImportResult(rows, issues);
    }

    private static Stream OpenFallbackFont()
    {
        const string resourceName = "ClosedXML.Graphics.Fonts.CarlitoBare-Regular.ttf";
        return typeof(DefaultGraphicEngine).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"ClosedXML fallback font resource '{resourceName}' was not found.");
    }

    private static void ImportWorksheet(
        IXLWorksheet worksheet,
        ICollection<ImportedPayrollRow> rows,
        ICollection<WorkbookImportIssue> issues)
    {
        HeaderMap? headers = TryReadHeaders(worksheet);
        if (headers is null)
        {
            issues.Add(new WorkbookImportIssue(
                ImportIssueSeverity.Warning,
                "UnsupportedWorksheetLayout",
                "The worksheet does not contain the required payroll category headers in row 1 and was skipped.",
                worksheet.Name));
            return;
        }

        int lastRowNumber = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        for (int rowNumber = 2; rowNumber <= lastRowNumber; rowNumber++)
        {
            IXLRow row = worksheet.Row(rowNumber);
            if (!HasSourceContent(row, headers))
            {
                continue;
            }

            ImportedPayrollRow? importedRow = TryImportRow(worksheet, row, rowNumber, headers, issues);
            if (importedRow is not null)
            {
                rows.Add(importedRow);
            }
        }
    }

    private static HeaderMap? TryReadHeaders(IXLWorksheet worksheet)
    {
        IXLRow headerRow = worksheet.Row(1);
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (IXLCell cell in headerRow.CellsUsed())
        {
            string header = NormalizeHeader(cell.GetString());
            if (header.Length > 0 && !columns.ContainsKey(header))
            {
                columns[header] = cell.Address.ColumnNumber;
            }
        }

        var missingHeaders = RequiredHeaders
            .Select(NormalizeHeader)
            .Where(header => !columns.ContainsKey(header))
            .ToArray();

        if (missingHeaders.Length > 0)
        {
            return null;
        }

        return new HeaderMap(
            columns[NormalizeHeader("Date")],
            columns[NormalizeHeader("Employee Number")],
            columns[NormalizeHeader("Employee Name")],
            columns[NormalizeHeader("Normal")],
            columns[NormalizeHeader("Overtime 1.5")],
            columns[NormalizeHeader("Sunday 0.5")],
            columns[NormalizeHeader("PH")],
            columns[NormalizeHeader("Night Shift")],
            columns[NormalizeHeader("Sick Leave")],
            columns[NormalizeHeader("Comment")]);
    }

    private static ImportedPayrollRow? TryImportRow(
        IXLWorksheet worksheet,
        IXLRow row,
        int rowNumber,
        HeaderMap headers,
        ICollection<WorkbookImportIssue> issues)
    {
        var rowIssues = new List<WorkbookImportIssue>();

        string periodLabel = worksheet.Cell(rowNumber, headers.DateColumn).GetString().Trim();
        (DateOnly? startDate, DateOnly? endDate) = ParsePeriod(
            periodLabel,
            worksheet.Name,
            rowNumber,
            headers.DateColumn,
            rowIssues);

        string employeeNumber = worksheet.Cell(rowNumber, headers.EmployeeNumberColumn).GetString().Trim();
        RequireText(
            employeeNumber,
            "MissingEmployeeNumber",
            "Employee Number is required.",
            worksheet,
            rowNumber,
            headers.EmployeeNumberColumn,
            rowIssues);

        string employeeName = worksheet.Cell(rowNumber, headers.EmployeeNameColumn).GetString().Trim();
        RequireText(
            employeeName,
            "MissingEmployeeName",
            "Employee Name is required.",
            worksheet,
            rowNumber,
            headers.EmployeeNameColumn,
            rowIssues);

        PayrollCellValue normal = ReadPayrollValue(
            worksheet,
            rowNumber,
            headers.NormalColumn,
            "Normal",
            rowIssues);
        PayrollCellValue overtime = ReadPayrollValue(
            worksheet,
            rowNumber,
            headers.OvertimeColumn,
            "Overtime 1.5",
            rowIssues);
        PayrollCellValue sunday = ReadPayrollValue(
            worksheet,
            rowNumber,
            headers.SundayColumn,
            "Sunday 0.5",
            rowIssues);
        PayrollCellValue publicHoliday = ReadPayrollValue(
            worksheet,
            rowNumber,
            headers.PublicHolidayColumn,
            "PH",
            rowIssues);
        PayrollCellValue nightShift = ReadPayrollValue(
            worksheet,
            rowNumber,
            headers.NightShiftColumn,
            "Night Shift",
            rowIssues);
        PayrollCellValue sickLeave = ReadPayrollValue(
            worksheet,
            rowNumber,
            headers.SickLeaveColumn,
            "Sick Leave",
            rowIssues);

        issues.AddMany(rowIssues);
        if (rowIssues.Any(issue => issue.Severity == ImportIssueSeverity.Error))
        {
            return null;
        }

        return new ImportedPayrollRow(
            worksheet.Name,
            rowNumber,
            periodLabel,
            startDate!.Value,
            endDate!.Value,
            employeeNumber,
            employeeName,
            normal,
            overtime,
            sunday,
            publicHoliday,
            nightShift,
            sickLeave,
            ReadOptionalText(worksheet.Cell(rowNumber, headers.CommentColumn)));
    }

    private static PayrollCellValue ReadPayrollValue(
        IXLWorksheet worksheet,
        int rowNumber,
        int columnNumber,
        string columnName,
        ICollection<WorkbookImportIssue> issues)
    {
        IXLCell cell = worksheet.Cell(rowNumber, columnNumber);
        string? numberFormat = cell.Style.NumberFormat.Format;

        if (cell.IsEmpty())
        {
            return new PayrollCellValue(null, numberFormat);
        }

        if (cell.DataType == XLDataType.Number && cell.TryGetValue<decimal>(out decimal numericValue))
        {
            return CreatePayrollValue(
                numericValue,
                numberFormat,
                columnName,
                worksheet,
                rowNumber,
                cell,
                issues);
        }

        if (cell.DataType == XLDataType.TimeSpan && cell.TryGetValue<TimeSpan>(out TimeSpan timeValue))
        {
            return CreatePayrollValue(
                (decimal)timeValue.TotalDays,
                numberFormat,
                columnName,
                worksheet,
                rowNumber,
                cell,
                issues);
        }

        string rawText = cell.GetString().Trim();
        if (decimal.TryParse(
                rawText,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out decimal parsedValue))
        {
            if (parsedValue < 0m)
            {
                issues.Add(new WorkbookImportIssue(
                    ImportIssueSeverity.Warning,
                    "NegativePayrollValue",
                    $"{columnName} contains a negative value, indicating a valid Prorata week where roster input is used instead of actual hours.",
                    worksheet.Name,
                    rowNumber,
                    cell.Address.ToString()));
            }

            return new PayrollCellValue(parsedValue, numberFormat);
        }

        issues.Add(new WorkbookImportIssue(
            ImportIssueSeverity.Error,
            "InvalidPayrollValue",
            $"{columnName} must contain a numeric value or be blank.",
            worksheet.Name,
            rowNumber,
            cell.Address.ToString()));
        return new PayrollCellValue(null, numberFormat);
    }

    private static PayrollCellValue CreatePayrollValue(
        decimal rawValue,
        string? numberFormat,
        string columnName,
        IXLWorksheet worksheet,
        int rowNumber,
        IXLCell cell,
        ICollection<WorkbookImportIssue> issues)
    {
        if (rawValue < 0m)
        {
            issues.Add(new WorkbookImportIssue(
                ImportIssueSeverity.Warning,
                "NegativePayrollValue",
                $"{columnName} contains a negative value, indicating a valid Prorata week where roster input is used instead of actual hours.",
                worksheet.Name,
                rowNumber,
                cell.Address.ToString()));
        }

        return new PayrollCellValue(rawValue, numberFormat);
    }

    private static (DateOnly? StartDate, DateOnly? EndDate) ParsePeriod(
        string periodLabel,
        string sheetName,
        int rowNumber,
        int columnNumber,
        ICollection<WorkbookImportIssue> issues)
    {
        Match match = PeriodLabelPattern.Match(periodLabel);
        if (!match.Success ||
            !DateOnly.TryParseExact(match.Groups["start"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly startDate) ||
            !DateOnly.TryParseExact(match.Groups["end"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly endDate) ||
            endDate < startDate)
        {
            issues.Add(new WorkbookImportIssue(
                ImportIssueSeverity.Error,
                "InvalidPeriod",
                "Date must be a valid period in yyyy-MM-dd - yyyy-MM-dd format.",
                sheetName,
                rowNumber,
                CellAddress(columnNumber, rowNumber)));
            return (null, null);
        }

        return (startDate, endDate);
    }

    private static void RequireText(
        string value,
        string code,
        string message,
        IXLWorksheet worksheet,
        int rowNumber,
        int columnNumber,
        ICollection<WorkbookImportIssue> issues)
    {
        if (value.Length == 0)
        {
            issues.Add(new WorkbookImportIssue(
                ImportIssueSeverity.Error,
                code,
                message,
                worksheet.Name,
                rowNumber,
                CellAddress(columnNumber, rowNumber)));
        }
    }

    private static bool HasSourceContent(IXLRow row, HeaderMap headers)
    {
        int[] columns =
        {
            headers.DateColumn,
            headers.EmployeeNumberColumn,
            headers.EmployeeNameColumn,
            headers.NormalColumn,
            headers.OvertimeColumn,
            headers.SundayColumn,
            headers.PublicHolidayColumn,
            headers.NightShiftColumn,
            headers.SickLeaveColumn,
            headers.CommentColumn
        };

        return columns.Any(column => !row.Cell(column).IsEmpty());
    }

    private static string? ReadOptionalText(IXLCell cell)
    {
        string value = cell.GetString().Trim();
        return value.Length == 0 ? null : value;
    }

    private static string NormalizeHeader(string value)
    {
        string normalized = string.Concat(value.Trim().Where(character => !char.IsWhiteSpace(character)));
        return normalized.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
               normalized.StartsWith("Comment", StringComparison.OrdinalIgnoreCase)
            ? normalized[..^1]
            : normalized;
    }

    private static string CellAddress(int columnNumber, int rowNumber)
    {
        int remaining = columnNumber;
        var letters = new Stack<char>();
        while (remaining > 0)
        {
            remaining--;
            letters.Push((char)('A' + remaining % 26));
            remaining /= 26;
        }

        return $"{new string(letters.ToArray())}{rowNumber}";
    }

    private sealed record HeaderMap(
        int DateColumn,
        int EmployeeNumberColumn,
        int EmployeeNameColumn,
        int NormalColumn,
        int OvertimeColumn,
        int SundayColumn,
        int PublicHolidayColumn,
        int NightShiftColumn,
        int SickLeaveColumn,
        int CommentColumn);
}

internal static class CollectionExtensions
{
    public static void AddMany<T>(this ICollection<T> collection, IEnumerable<T> values)
    {
        foreach (T value in values)
        {
            collection.Add(value);
        }
    }
}
