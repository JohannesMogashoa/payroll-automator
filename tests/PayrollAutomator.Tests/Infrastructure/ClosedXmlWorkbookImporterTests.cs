using System.Security.Cryptography;
using ClosedXML.Excel;
using PayrollAutomator.Application;
using PayrollAutomator.Infrastructure;
using Xunit;

namespace PayrollAutomator.Tests.Infrastructure;

public sealed class ClosedXmlWorkbookImporterTests
{
    [Fact]
    public void Import_RecognizesWeeklyAndConsolidatedLayouts_AndReportsUnsupportedSheets()
    {
        string path = CreateWorkbook(CreateValidWorkbook);
        try
        {
            byte[] before = SHA256.HashData(File.ReadAllBytes(path));

            WorkbookImportResult result = new ClosedXmlWorkbookImporter().Import(path);

            byte[] after = SHA256.HashData(File.ReadAllBytes(path));
            Assert.Equal(before, after);
            Assert.False(result.HasErrors, string.Join(" | ", result.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            Assert.Equal(3, result.Rows.Count);
            Assert.Contains(result.Rows, row => row.SheetName == "Weekly" && row.SourceRowNumber == 2);
            Assert.Contains(result.Rows, row => row.SheetName == "Olivedale Employees" && row.SourceRowNumber == 2);
            Assert.Contains(result.Issues, issue => issue.Code == "UnsupportedWorksheetLayout" && issue.SheetName == "Sheet4");

            ImportedPayrollRow weeklyRow = Assert.Single(result.Rows, row => row.SheetName == "Weekly" && row.SourceRowNumber == 2);
            Assert.Equal("F001", weeklyRow.EmployeeNumber);
            Assert.Equal("Fictional Employee", weeklyRow.EmployeeName);
            Assert.Equal(new DateOnly(2026, 10, 5), weeklyRow.PeriodStartDate);
            Assert.Equal(new DateOnly(2026, 10, 11), weeklyRow.PeriodEndDate);
            Assert.Equal(1.875m, weeklyRow.Normal.RawValue);
            Assert.Equal(0.2m, weeklyRow.Overtime.RawValue);
            Assert.Equal("h:mm", weeklyRow.Overtime.NumberFormat);
        }
        finally
        {
            DeleteTemporaryWorkbook(path);
        }
    }

    [Fact]
    public void Import_PreservesNegativeValuesAndReportsProrataWarnings()
    {
        string path = CreateWorkbook(workbook =>
        {
            IXLWorksheet sheet = workbook.Worksheets.Add("Weekly");
            AddHeaders(sheet, employeeNameColumn: 3, commentHeader: "Comment");
            AddDataRow(sheet, 2, 3, "F002", "Another Fictional Employee", -0.5m, 0.1m);
        });

        try
        {
            WorkbookImportResult result = new ClosedXmlWorkbookImporter().Import(path);

            Assert.False(result.HasErrors, string.Join(" | ", result.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            PayrollCellValue normal = Assert.Single(result.Rows).Normal;
            Assert.Equal(-0.5m, normal.RawValue);
            Assert.Contains(result.Issues, issue =>
                issue.Code == "NegativePayrollValue" &&
                issue.Severity == ImportIssueSeverity.Warning &&
                issue.SourceRowNumber == 2);
        }
        finally
        {
            DeleteTemporaryWorkbook(path);
        }
    }

    [Fact]
    public void Import_ReturnsRowErrorsWithSheetRowAndCellContext()
    {
        string path = CreateWorkbook(workbook =>
        {
            IXLWorksheet sheet = workbook.Worksheets.Add("Weekly");
            AddHeaders(sheet, employeeNameColumn: 3, commentHeader: "Comments");
            AddDataRow(sheet, 2, 3, "F003", "", 1.0m, 0m);
            sheet.Cell("A3").Value = "not-a-period";
            sheet.Cell("B3").Value = "F004";
            sheet.Cell("C3").Value = "Fictional Invalid Row";
            sheet.Cell("D3").Value = "not-a-number";
        });

        try
        {
            WorkbookImportResult result = new ClosedXmlWorkbookImporter().Import(path);

            Assert.True(result.HasErrors);
            Assert.Empty(result.Rows);
            Assert.Contains(result.Issues, issue =>
                issue.Code == "MissingEmployeeName" &&
                issue.SheetName == "Weekly" &&
                issue.SourceRowNumber == 2 &&
                issue.CellAddress == "C2");
            Assert.Contains(result.Issues, issue =>
                issue.Code == "InvalidPeriod" &&
                issue.SourceRowNumber == 3 &&
                issue.CellAddress == "A3");
            Assert.Contains(result.Issues, issue => issue.Code == "InvalidPayrollValue" && issue.CellAddress == "D3");
        }
        finally
        {
            DeleteTemporaryWorkbook(path);
        }
    }

    private static string CreateWorkbook(Action<XLWorkbook> configure)
    {
        string directory = Path.Combine(Path.GetTempPath(), "payroll-automator-tests");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"fixture-{Guid.NewGuid():N}.xlsx");

        using (var workbook = new XLWorkbook())
        {
            configure(workbook);
            workbook.SaveAs(path);
        }

        return path;
    }

    private static void CreateValidWorkbook(XLWorkbook workbook)
    {
        IXLWorksheet weekly = workbook.Worksheets.Add("Weekly");
        AddHeaders(weekly, employeeNameColumn: 3, commentHeader: "Comment");
        AddDataRow(weekly, 2, 3, "F001", "Fictional Employee", 1.875m, 0.2m);
        AddDataRow(weekly, 3, 3, "F002", "Second Fictional Employee", 1.5m, 0m);

        IXLWorksheet consolidated = workbook.Worksheets.Add("Olivedale Employees");
        AddHeaders(consolidated, employeeNameColumn: 4, commentHeader: "Comments");
        AddDataRow(consolidated, 2, 4, "F003", "Consolidated Fictional Employee", 1.25m, 0.125m);

        workbook.Worksheets.Add("Sheet4").Cell("A7").Value = "unheaded data";
    }

    private static void AddHeaders(IXLWorksheet sheet, int employeeNameColumn, string commentHeader)
    {
        sheet.Cell(1, 1).Value = "Date";
        sheet.Cell(1, 2).Value = "Employee Number";
        sheet.Cell(1, employeeNameColumn).Value = "Employee Name";
        int normalColumn = employeeNameColumn + 1;
        sheet.Cell(1, normalColumn).Value = "Normal";
        sheet.Cell(1, normalColumn + 1).Value = "Overtime 1.5";
        sheet.Cell(1, normalColumn + 2).Value = "Sunday 0.5";
        sheet.Cell(1, normalColumn + 3).Value = "PH";
        sheet.Cell(1, normalColumn + 4).Value = "Night Shift";
        sheet.Cell(1, normalColumn + 5).Value = "Sick Leave";
        sheet.Cell(1, normalColumn + 6).Value = commentHeader;
    }

    private static void AddDataRow(
        IXLWorksheet sheet,
        int rowNumber,
        int employeeNameColumn,
        string employeeNumber,
        string employeeName,
        decimal normal,
        decimal overtime)
    {
        int normalColumn = employeeNameColumn + 1;
        sheet.Cell(rowNumber, 1).Value = "2026-10-05 - 2026-10-11";
        sheet.Cell(rowNumber, 2).Value = employeeNumber;
        sheet.Cell(rowNumber, employeeNameColumn).Value = employeeName;
        sheet.Cell(rowNumber, normalColumn).Value = normal;
        sheet.Cell(rowNumber, normalColumn + 1).Value = overtime;
        sheet.Cell(rowNumber, normalColumn + 1).Style.NumberFormat.Format = "h:mm";
        sheet.Cell(rowNumber, normalColumn + 2).Value = 0m;
        sheet.Cell(rowNumber, normalColumn + 3).Value = 0m;
        sheet.Cell(rowNumber, normalColumn + 4).Value = 0m;
        sheet.Cell(rowNumber, normalColumn + 5).Value = 0m;
    }

    private static void DeleteTemporaryWorkbook(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
