using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PayrollAutomator.Application;
using PayrollAutomator.Domain;
using PayrollAutomator.Infrastructure;
using Xunit;

namespace PayrollAutomator.Tests.Application;

public sealed class PayrollRunServiceTests
{
    [Fact]
    public async Task RunLifecycle_PersistsReviewExportsAndDeletesArtifacts()
    {
        string dataRoot = Path.Combine(
            Path.GetTempPath(),
            "payroll-automator-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataRoot);

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PayrollDbContext>()
            .UseSqlite(connection)
            .Options;

        try
        {
            await using (var db = new PayrollDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
                PayrollRunService service = CreateService(
                    db,
                    dataRoot,
                    new TestWorkbookImporter());

                PayrollRunSummary created = await service.CreateRunAsync(
                    new MemoryStream([1, 2, 3]),
                    "synthetic-payroll.xlsx");

                Assert.Equal(PayrollRunStatus.NeedsReview, created.Status);
                PayrollRunDetails details = await service.GetRunAsync(created.Id)
                    ?? throw new InvalidOperationException("Created run was not persisted.");
                Assert.Equal(8m, details.Review.IncludedPayrollHours);
                Assert.Single(details.AuditEntries);

                await service.ConfirmAndExportAsync(created.Id, "Fictional Reviewer");
                PayrollRunDetails exported = await service.GetRunAsync(created.Id)
                    ?? throw new InvalidOperationException("Exported run was not persisted.");
                Assert.Equal(PayrollRunStatus.Exported, exported.Summary.Status);
                Assert.Equal(2, exported.AuditEntries.Count);

                PayrollDownload download = await service.OpenExportAsync(created.Id)
                    ?? throw new InvalidOperationException("Export was not persisted.");
                await using (download.Content)
                {
                    Assert.Equal("payroll-export.xlsx", download.FileName);
                }

                await service.DeleteRunAsync(created.Id);
                PayrollRunSummary deleted = Assert.Single(await service.ListRunsAsync());
                Assert.Equal(PayrollRunStatus.Deleted, deleted.Status);
                Assert.False(deleted.HasExport);
                Assert.Null(await service.GetRunAsync(created.Id));
            }
        }
        finally
        {
            if (Directory.Exists(dataRoot))
            {
                Directory.Delete(dataRoot, recursive: true);
            }
        }
    }

    private static PayrollRunService CreateService(
        PayrollDbContext db,
        string dataRoot,
        IWorkbookImporter importer) =>
        new(
            new EfPayrollRunRepository(db),
            new LocalPayrollFileStore(new PayrollStorageOptions { DataRoot = dataRoot }),
            importer,
            new TestWorkbookExporter());

    private sealed class TestWorkbookImporter
        : IWorkbookImporter
    {
        public WorkbookImportResult Import(string sourcePath) =>
            new(
            [
                new ImportedPayrollRow(
                    "Weekly",
                    2,
                    "2026-10-05 - 2026-10-11",
                    new DateOnly(2026, 10, 5),
                    new DateOnly(2026, 10, 11),
                    "F001",
                    "Fictional Employee",
                    new PayrollCellValue(8m, "General"),
                    new PayrollCellValue(null, null),
                    new PayrollCellValue(null, null),
                    new PayrollCellValue(null, null),
                    new PayrollCellValue(null, null),
                    new PayrollCellValue(null, null),
                    null)
            ],
            []);
    }

    private sealed class TestWorkbookExporter : IPayrollWorkbookExporter
    {
        public PayrollExportResult Export(PayrollExportRequest request)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(request.OutputPath)!);
            File.WriteAllBytes(request.OutputPath, [1, 2, 3]);
            return new PayrollExportResult(request.OutputPath, []);
        }
    }
}
