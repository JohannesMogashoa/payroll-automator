using PayrollAutomator.Application;
using PayrollAutomator.Infrastructure;

Console.WriteLine("Payroll Automator");

if (args.Length == 2 && args[0].Equals("import", StringComparison.OrdinalIgnoreCase))
{
    var application = new PayrollApplication(new ClosedXmlWorkbookImporter());
    WorkbookImportResult result = application.ImportWorkbook(args[1]);

    Console.WriteLine($"Imported rows: {result.Rows.Count}");
    Console.WriteLine($"Errors: {result.Issues.Count(issue => issue.Severity == ImportIssueSeverity.Error)}");
    Console.WriteLine($"Warnings: {result.Issues.Count(issue => issue.Severity == ImportIssueSeverity.Warning)}");

    foreach (WorkbookImportIssue issue in result.Issues)
    {
        string location = issue.SourceRowNumber is null
            ? issue.SheetName
            : $"{issue.SheetName}, row {issue.SourceRowNumber}, {issue.CellAddress}";
        Console.WriteLine($"{issue.Severity}: {issue.Code} ({location}) {issue.Message}");
    }

    return;
}

if (args.Length == 5 && args[0].Equals("export", StringComparison.OrdinalIgnoreCase) &&
    args[3].Equals("--confirm", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        var application = new PayrollApplication(
            new ClosedXmlWorkbookImporter(),
            new ClosedXmlPayrollWorkbookExporter());
        WorkbookImportResult importResult = application.ImportWorkbook(args[1]);
        PayrollCalculationResult calculation = application.CalculateWorkbook(importResult);
        PayrollReviewSummary review = application.ReviewCalculation(calculation);

        Console.WriteLine($"${review.CalculatedRowCount}");
        Console.WriteLine($"${review.ErrorCount}");
        Console.WriteLine($"${review.WarningCount}");

        foreach (PayrollReviewException exception in review.Exceptions)
        {
            string location = exception.SourceRowNumber is null
                ? exception.SheetName
                : $"${exception.SheetName}, row ${exception.SourceRowNumber}";
            if (!string.IsNullOrWhiteSpace(exception.CellAddress))
            {
                location += $", ${exception.CellAddress}";
            }

            Console.WriteLine($"${exception.Severity}: ${exception.Code} (${location}) ${exception.Message}");
        }

        ReviewConfirmation confirmation = application.ConfirmReview(review, args[4]);
        PayrollExportResult export = application.ExportWorkbook(
            args[1],
            args[2],
            calculation,
            review,
            confirmation);

        Console.WriteLine($"Exported: ${export.OutputPath}");
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or FileNotFoundException)
    {
        Console.Error.WriteLine($"Export failed: ${exception.Message}");
        Environment.ExitCode = 1;
    }

    return;
}

Console.WriteLine("Usage:");
Console.WriteLine("  dotnet run --project src/PayrollAutomator -- import <workbook.xlsx>");
Console.WriteLine("  dotnet run --project src/PayrollAutomator -- export <input.xlsx> <output.xlsx> --confirm <reviewer>");
