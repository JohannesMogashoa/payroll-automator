using System.Security.Cryptography;
using ClosedXML.Excel;
using ClosedXML.Graphics;
using PayrollAutomator.Application;
using PayrollAutomator.Domain;
using PayrollAutomator.Infrastructure;
using Xunit;

namespace PayrollAutomator.Tests.Infrastructure;

public sealed class ClosedXmlPayrollWorkbookExporterTests
{
    [Fact]
    public void Export_CreatesAuditableWorkbookWithoutChangingSource()
    {
        string directory = CreateTemporaryDirectory();
        string sourcePath = Path.Combine(directory, "source.xlsx");
        string outputPath = Path.Combine(directory, "output", "payroll-export.xlsx");
        CreateSourceWorkbook(sourcePath);

        try
        {
            byte[] before = SHA256.HashData(File.ReadAllBytes(sourcePath));
            var application = new PayrollApplication(
                new ClosedXmlWorkbookImporter(),
                new ClosedXmlPayrollWorkbookExporter());
            WorkbookImportResult import = application.ImportWorkbook(sourcePath);
            PayrollCalculationResult calculation = application.CalculateWorkbook(import);
            PayrollReviewSummary review = application.ReviewCalculation(calculation);
            ReviewConfirmation confirmation = application.ConfirmReview(
                review,
                "Fictional Reviewer",
                confirmedAtUtc: new DateTimeOffset(2026, 10, 27, 8, 0, 0, TimeSpan.Zero));

            PayrollExportResult result = application.ExportWorkbook(
                sourcePath,
                outputPath,
                calculation,
                review,
                confirmation);

            byte[] after = SHA256.HashData(File.ReadAllBytes(sourcePath));
            Assert.Equal(before, after);
            Assert.Equal(outputPath, result.OutputPath);
            Assert.True(File.Exists(outputPath));

            using Stream fallbackFont = OpenFallbackFont();
            var loadOptions = new LoadOptions
            {
                GraphicEngine = DefaultGraphicEngine.CreateOnlyWithFonts(fallbackFont)
            };
            using var workbook = new XLWorkbook(outputPath, loadOptions);
            Assert.Equal(
                new[] { "Payroll", "Employee Totals", "Review", "Metadata", "Audit" },
                workbook.Worksheets.Select(sheet => sheet.Name));
            Assert.Equal("F001", workbook.Worksheet("Payroll").Cell("D2").GetString());
            Assert.Equal("TOTAL", workbook.Worksheet("Employee Totals").Cell("A3").GetString());
            Assert.Equal(8m, workbook.Worksheet("Employee Totals").Cell("C3").GetValue<decimal>());
            Assert.Equal(1560m, workbook.Worksheet("Employee Totals").Cell("D3").GetValue<decimal>());
            Assert.Equal(
                PayrollRules.RuleSetVersion,
                workbook.Worksheet("Metadata").Cell("B4").GetString());
            Assert.Equal(0, workbook.Worksheet("Review").Cell("B7").GetValue<int>());
            Assert.Equal(3, workbook.Worksheet("Audit").LastRowUsed()!.RowNumber());
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [Fact]
    public void Export_RejectsTheSourcePathAsOutputPath()
    {
        string directory = CreateTemporaryDirectory();
        string sourcePath = Path.Combine(directory, "source.xlsx");
        CreateSourceWorkbook(sourcePath);

        try
        {
            var application = new PayrollApplication(
                new ClosedXmlWorkbookImporter(),
                new ClosedXmlPayrollWorkbookExporter());
            WorkbookImportResult import = application.ImportWorkbook(sourcePath);
            PayrollCalculationResult calculation = application.CalculateWorkbook(import);
            PayrollReviewSummary review = application.ReviewCalculation(calculation);
            ReviewConfirmation confirmation = application.ConfirmReview(review, "Fictional Reviewer");

            Assert.Throws<InvalidOperationException>(() =>
                application.ExportWorkbook(
                    sourcePath,
                    sourcePath,
                    calculation,
                    review,
                    confirmation));
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    private static void CreateSourceWorkbook(string path)
    {
        using var workbook = new XLWorkbook();
        IXLWorksheet sheet = workbook.Worksheets.Add("Weekly");
        string[] headers =
        [
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
        ];

        for (int column = 0; column < headers.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = headers[column];
        }

        sheet.Cell("A2").Value = "2026-10-05 - 2026-10-11";
        sheet.Cell("B2").Value = "F001";
        sheet.Cell("C2").Value = "Fictional Employee";
        sheet.Cell("D2").Value = 8m;
        sheet.Cell("E2").Value = 0m;
        sheet.Cell("F2").Value = 0m;
        sheet.Cell("G2").Value = 0m;
        sheet.Cell("H2").Value = 0m;
        sheet.Cell("I2").Value = 0m;
        workbook.SaveAs(path);
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "payroll-automator-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteTemporaryDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Stream OpenFallbackFont()
    {
        const string resourceName = "ClosedXML.Graphics.Fonts.CarlitoBare-Regular.ttf";
        return typeof(DefaultGraphicEngine).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("ClosedXML fallback font resource was not found.");
    }
}
