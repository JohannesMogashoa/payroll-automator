using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PayrollAutomator.Application;
using PayrollAutomator.Components;
using PayrollAutomator.Infrastructure;

if (args.Length > 0 &&
    (args[0].Equals("import", StringComparison.OrdinalIgnoreCase) ||
     args[0].Equals("export", StringComparison.OrdinalIgnoreCase)))
{
    await CliRunner.RunAsync(args);
    return;
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services
    .AddOptions<PayrollStorageOptions>()
    .Bind(builder.Configuration.GetSection("Payroll"))
    .Validate(options => PayrollAutomationConfiguration.Validate(options).Count == 0,
        "Payroll storage and automation configuration is invalid.")
    .ValidateOnStart();
builder.Services.AddSingleton(serviceProvider =>
    serviceProvider.GetRequiredService<IOptions<PayrollStorageOptions>>().Value);
string configuredDataRoot = builder.Configuration["Payroll:DataRoot"] ?? "data";
string keyDirectory = Path.Combine(Path.GetFullPath(configuredDataRoot), "keys");
Directory.CreateDirectory(keyDirectory);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory))
    .SetApplicationName("PayrollAutomator");

builder.Services.AddDbContext<PayrollDbContext>((serviceProvider, options) =>
{
    PayrollStorageOptions storage = serviceProvider.GetRequiredService<PayrollStorageOptions>();
    Directory.CreateDirectory(storage.DataRoot);
    options.UseSqlite($"Data Source={Path.Combine(storage.DataRoot, "payroll.db")}");
});
builder.Services.AddScoped<IPayrollRunRepository, EfPayrollRunRepository>();
builder.Services.AddSingleton<IPayrollFileStore, LocalPayrollFileStore>();
builder.Services.AddSingleton<IWorkbookImporter, ClosedXmlWorkbookImporter>();
builder.Services.AddSingleton<IPayrollWorkbookExporter, ClosedXmlPayrollWorkbookExporter>();
builder.Services.AddScoped<PayrollRunService>();
builder.Services.AddHealthChecks();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

WebApplication app = builder.Build();
using (IServiceScope scope = app.Services.CreateScope())
{
    PayrollStorageOptions storage = scope.ServiceProvider.GetRequiredService<PayrollStorageOptions>();
    Directory.CreateDirectory(storage.DataRoot);
    scope.ServiceProvider.GetRequiredService<PayrollDbContext>().Database.EnsureCreated();
}

app.UseStaticFiles();
app.UseAntiforgery();
app.MapHealthChecks("/healthz");
app.MapGet(
    "/runs/{runId:guid}/download",
    async (Guid runId, PayrollRunService runs, CancellationToken cancellationToken) =>
    {
        PayrollDownload? download = await runs.OpenExportAsync(runId, cancellationToken);
        return download is null
            ? Results.NotFound()
            : Results.File(
                download.Content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                download.FileName);
    });
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();

internal static class CliRunner
{
    public static async Task RunAsync(string[] args)
    {
        if (args.Length == 2 && args[0].Equals("import", StringComparison.OrdinalIgnoreCase))
        {
            var application = new PayrollApplication(new ClosedXmlWorkbookImporter());
            WorkbookImportResult result = application.ImportWorkbook(args[1]);

            Console.WriteLine($"Imported rows: {result.Rows.Count}");
            Console.WriteLine($"Errors: {result.Issues.Count(issue => issue.Severity == ImportIssueSeverity.Error)}");
            Console.WriteLine($"Warnings: {result.Issues.Count(issue => issue.Severity == ImportIssueSeverity.Warning)}");
            foreach (WorkbookImportIssue issue in result.Issues)
            {
                Console.WriteLine($"{issue.Severity}: {issue.Code} ({FormatLocation(issue.SheetName, issue.SourceRowNumber, issue.CellAddress)}) {issue.Message}");
            }

            return;
        }

        if (args.Length == 5 &&
            args[0].Equals("export", StringComparison.OrdinalIgnoreCase) &&
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

                Console.WriteLine($"Rows: {review.CalculatedRowCount}");
                Console.WriteLine($"Errors: {review.ErrorCount}");
                Console.WriteLine($"Warnings: {review.WarningCount}");
                foreach (PayrollReviewException exception in review.Exceptions)
                {
                    Console.WriteLine($"{exception.Severity}: {exception.Code} ({FormatLocation(exception.SheetName, exception.SourceRowNumber, exception.CellAddress)}) {exception.Message}");
                }

                ReviewConfirmation confirmation = application.ConfirmReview(review, args[4]);
                PayrollExportResult export = application.ExportWorkbook(
                    args[1],
                    args[2],
                    calculation,
                    review,
                    confirmation);
                Console.WriteLine($"Exported: {export.OutputPath}");
            }
            catch (Exception exception) when (
                exception is ArgumentException or
                InvalidOperationException or
                IOException or
                FileNotFoundException)
            {
                Console.Error.WriteLine($"Export failed: {exception.Message}");
                Environment.ExitCode = 1;
            }

            return;
        }

        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project src/PayrollAutomator -- import <workbook.xlsx>");
        Console.WriteLine("  dotnet run --project src/PayrollAutomator -- export <input.xlsx> <output.xlsx> --confirm <reviewer>");
        await Task.CompletedTask;
    }

    private static string FormatLocation(string sheetName, int? rowNumber, string? cellAddress)
    {
        string location = rowNumber is null ? sheetName : $"{sheetName}, row {rowNumber}";
        return string.IsNullOrWhiteSpace(cellAddress) ? location : $"{location}, {cellAddress}";
    }
}
