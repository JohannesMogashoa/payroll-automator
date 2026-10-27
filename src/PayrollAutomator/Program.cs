using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using PayrollAutomator.Application;
using PayrollAutomator.Components;
using PayrollAutomator.Infrastructure;

if (args.Length > 0 &&
    (args[0].Equals("import", StringComparison.OrdinalIgnoreCase) ||
     args[0].Equals("export", StringComparison.OrdinalIgnoreCase) ||
     args[0].Equals("process", StringComparison.OrdinalIgnoreCase) ||
     args[0].Equals("scan", StringComparison.OrdinalIgnoreCase) ||
     args[0].Equals("cleanup", StringComparison.OrdinalIgnoreCase) ||
     args[0].Equals("backup", StringComparison.OrdinalIgnoreCase)))
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
builder.Services.AddScoped<IAutomationJobRepository, EfAutomationJobRepository>();
builder.Services.AddScoped<IWorkbookDiscovery, LocalWorkbookDiscovery>();
builder.Services.AddHttpClient("PayrollAutomationWebhook", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PayrollAutomator", "1.0"));
});
builder.Services.AddScoped<IPayrollAutomationNotificationSender>(serviceProvider =>
{
    PayrollStorageOptions storage = serviceProvider.GetRequiredService<PayrollStorageOptions>();
    return string.Equals(storage.Automation.NotificationProvider, "Webhook", StringComparison.OrdinalIgnoreCase)
        ? new WebhookPayrollAutomationNotificationSender(
            serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("PayrollAutomationWebhook"),
            storage)
        : string.Equals(storage.Automation.NotificationProvider, "None", StringComparison.OrdinalIgnoreCase)
            ? new NullPayrollAutomationNotificationSender()
            : new ConsolePayrollAutomationNotificationSender(
                serviceProvider.GetRequiredService<ILogger<ConsolePayrollAutomationNotificationSender>>());
});
builder.Services.AddScoped<PayrollAutomationNotificationDispatcher>();
builder.Services.AddScoped<PayrollAutomationProcessor>();
builder.Services.AddScoped<PayrollAutomationOperationsService>();
builder.Services.AddScoped<IPayrollBackupService, LocalPayrollBackupService>();
builder.Services.AddSingleton<IPayrollFileStore, LocalPayrollFileStore>();
builder.Services.AddSingleton<IWorkbookImporter, ClosedXmlWorkbookImporter>();
builder.Services.AddSingleton<IPayrollWorkbookExporter, ClosedXmlPayrollWorkbookExporter>();
builder.Services.AddScoped<PayrollRunService>();
builder.Services.AddScoped<IPayrollRunService>(serviceProvider =>
    serviceProvider.GetRequiredService<PayrollRunService>());
builder.Services.AddHostedService<PayrollAutomationWorker>();
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
        if (args.Length >= 2 && args[0].Equals("process", StringComparison.OrdinalIgnoreCase))
        {
            await RunAutomationAsync(args, scan: false);
            return;
        }

        if (args.Length >= 2 && args[0].Equals("scan", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 3 || !args[2].Equals("--once", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine("Scan requires --once.");
                Environment.ExitCode = 2;
                return;
            }

            await RunAutomationAsync(args, scan: true);
            return;
        }

        if (args.Length >= 2 && args[0].Equals("cleanup", StringComparison.OrdinalIgnoreCase))
        {
            await RunCleanupAsync(args);
            return;
        }

        if (args[0].Equals("backup", StringComparison.OrdinalIgnoreCase))
        {
            await RunBackupAsync(args);
            return;
        }

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
        Console.WriteLine("  dotnet run --project src/PayrollAutomator -- process <workbook.xlsx> [--data-root <directory>]");
        Console.WriteLine("  dotnet run --project src/PayrollAutomator -- scan <input-directory> --once [--data-root <directory>]");
        Console.WriteLine("  dotnet run --project src/PayrollAutomator -- cleanup --dry-run [--data-root <directory>]");
        Console.WriteLine("  dotnet run --project src/PayrollAutomator -- cleanup --apply [--data-root <directory>]");
        Console.WriteLine("  dotnet run --project src/PayrollAutomator -- backup [--data-root <directory>]");
    }

    private static async Task RunAutomationAsync(string[] args, bool scan)
    {
        try
        {
            string? dataRoot = ReadOption(args, "--data-root");
            PayrollStorageOptions storage = new()
            {
                DataRoot = dataRoot ?? "data"
            };
            Directory.CreateDirectory(storage.DataRoot);
            var dbOptions = new DbContextOptionsBuilder<PayrollDbContext>()
                .UseSqlite($"Data Source={Path.Combine(storage.DataRoot, "payroll.db")}")
                .Options;
            await using var db = new PayrollDbContext(dbOptions);
            await db.Database.EnsureCreatedAsync();
            var processor = new PayrollAutomationProcessor(
                storage,
                new EfAutomationJobRepository(db),
                new PayrollRunService(
                    new EfPayrollRunRepository(db),
                    new LocalPayrollFileStore(storage),
                    new ClosedXmlWorkbookImporter(),
                    new ClosedXmlPayrollWorkbookExporter()),
                new LocalWorkbookDiscovery());

            if (scan)
            {
                PayrollAutomationScanSummary summary = await processor.ScanOnceAsync(args[1]);
                Console.WriteLine($"Candidates: {summary.CandidateCount}");
                Console.WriteLine($"Processed: {summary.ProcessedCount}");
                Console.WriteLine($"Needs review: {summary.NeedsReviewCount}");
                Console.WriteLine($"Duplicates: {summary.DuplicateCount}");
                Console.WriteLine($"Waiting for stability: {summary.WaitingForStabilityCount}");
                Console.WriteLine($"Quarantined: {summary.QuarantinedCount}");
                Console.WriteLine($"Failed: {summary.FailedCount}");
                Environment.ExitCode = summary.HasFailures ? 1 : 0;
            }
            else
            {
                PayrollAutomationProcessResult result = await processor.ProcessFileAsync(args[1]);
                Console.WriteLine($"Result: {result.Code}");
                Console.WriteLine($"Status: {result.Status}");
                Console.WriteLine($"Run: {result.PayrollRunId?.ToString() ?? "none"}");
                Environment.ExitCode = result.Code == PayrollAutomationResultCode.Failed ? 1 : 0;
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or IOException or DbUpdateException)
        {
            Console.Error.WriteLine($"Automation failed: {exception.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static async Task RunCleanupAsync(string[] args)
    {
        try
        {
            bool dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
            bool apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
            if (dryRun == apply)
            {
                throw new ArgumentException("Cleanup requires exactly one of --dry-run or --apply.");
            }

            PayrollStorageOptions storage = CreateCliStorage(args);
            await using var db = await CreateCliDatabaseAsync(storage);
            var operations = CreateCliOperations(storage, db);
            PayrollRetentionPreview preview = await operations.PreviewRetentionAsync(DateTimeOffset.UtcNow);
            Console.WriteLine($"Eligible runs: {preview.EligibleRunCount}");
            Console.WriteLine($"Eligible jobs: {preview.EligibleJobCount}");
            if (apply)
            {
                PayrollRetentionApplyResult result = await operations.ApplyRetentionAsync(DateTimeOffset.UtcNow);
                Console.WriteLine($"Deleted runs: {result.DeletedRunCount}");
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or IOException or DbUpdateException)
        {
            Console.Error.WriteLine($"Cleanup failed: {exception.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static async Task RunBackupAsync(string[] args)
    {
        try
        {
            PayrollStorageOptions storage = CreateCliStorage(args);
            await using var db = await CreateCliDatabaseAsync(storage);
            IPayrollBackupService backup = new LocalPayrollBackupService(storage);
            PayrollBackupResult result = await backup.CreateAsync();
            await backup.VerifyAsync(result.BackupDirectory);
            Console.WriteLine($"Backup: {result.BackupDirectory}");
            Console.WriteLine($"Manifest: {result.ManifestPath}");
            Console.WriteLine($"Files: {result.FileCount}");
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or IOException or DbUpdateException)
        {
            Console.Error.WriteLine($"Backup failed: {exception.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static PayrollStorageOptions CreateCliStorage(string[] args) =>
        new() { DataRoot = ReadOption(args, "--data-root") ?? "data" };

    private static async Task<PayrollDbContext> CreateCliDatabaseAsync(PayrollStorageOptions storage)
    {
        Directory.CreateDirectory(storage.DataRoot);
        var dbOptions = new DbContextOptionsBuilder<PayrollDbContext>()
            .UseSqlite($"Data Source={Path.Combine(storage.DataRoot, "payroll.db")}")
            .Options;
        var db = new PayrollDbContext(dbOptions);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static PayrollAutomationOperationsService CreateCliOperations(
        PayrollStorageOptions storage,
        PayrollDbContext db)
    {
        var runService = new PayrollRunService(
            new EfPayrollRunRepository(db),
            new LocalPayrollFileStore(storage),
            new ClosedXmlWorkbookImporter(),
            new ClosedXmlPayrollWorkbookExporter());
        var processor = new PayrollAutomationProcessor(
            storage,
            new EfAutomationJobRepository(db),
            runService,
            new LocalWorkbookDiscovery());
        return new PayrollAutomationOperationsService(
            storage,
            new EfAutomationJobRepository(db),
            new EfPayrollRunRepository(db),
            processor,
            runService,
            new LocalPayrollBackupService(storage));
    }

    private static string? ReadOption(string[] args, string option)
    {
        int index = Array.FindIndex(args, value => value.Equals(option, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return null;
        }

        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Option '{option}' requires a value.");
        }

        return args[index + 1];
    }

    private static string FormatLocation(string sheetName, int? rowNumber, string? cellAddress)
    {
        string location = rowNumber is null ? sheetName : $"{sheetName}, row {rowNumber}";
        return string.IsNullOrWhiteSpace(cellAddress) ? location : $"{location}, {cellAddress}";
    }
}
