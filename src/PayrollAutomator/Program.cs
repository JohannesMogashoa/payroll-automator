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

Console.WriteLine("Usage: dotnet run --project src/PayrollAutomator -- import <workbook.xlsx>");
Console.WriteLine("Workbook import and calculation are implemented; review and export remain later phases.");
