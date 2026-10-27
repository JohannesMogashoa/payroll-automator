using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PayrollAutomator.Application;
using PayrollAutomator.Infrastructure;
using Xunit;

namespace PayrollAutomator.Tests.Application;

public sealed class PayrollAutomationOperationsServiceTests
{
    [Fact]
    public async Task GetStatusAsync_AggregatesJobStatesWithoutPayrollValues()
    {
        await using SqliteConnection connection = await OpenDatabaseAsync();
        await using var db = CreateDatabase(connection);
        db.AutomationJobs.AddRange(
            CreateJob(PayrollAutomationJobStatus.Queued, 1),
            CreateJob(PayrollAutomationJobStatus.NeedsReview, 2),
            CreateJob(PayrollAutomationJobStatus.Failed, 3),
            CreateJob(PayrollAutomationJobStatus.Quarantined, 4));
        await db.SaveChangesAsync();

        PayrollAutomationStatusSummary status = await CreateService(db).GetStatusAsync();

        Assert.False(status.Enabled);
        Assert.Equal(4, status.TotalJobs);
        Assert.Equal(1, status.QueuedJobs);
        Assert.Equal(1, status.NeedsReviewJobs);
        Assert.Equal(1, status.FailedJobs);
        Assert.Equal(1, status.QuarantinedJobs);
        Assert.NotNull(status.LastJobUpdatedAtUtc);
    }

    [Fact]
    public async Task PreviewRetentionAsync_ProtectsNeedsReviewAndDeletedRuns()
    {
        await using SqliteConnection connection = await OpenDatabaseAsync();
        await using var db = CreateDatabase(connection);
        DateTimeOffset now = new(2026, 10, 27, 8, 0, 0, TimeSpan.Zero);
        DateTimeOffset old = now.AddDays(-31);
        db.PayrollRuns.AddRange(
            new PayrollRunRecord
            {
                Id = Guid.NewGuid(),
                OriginalFileName = "old-export.xlsx",
                SourceRelativePath = "runs/old/source.xlsx",
                Status = PayrollRunStatus.Exported,
                CreatedAtUtc = old,
                UpdatedAtUtc = old,
                RuleSetVersion = "test"
            },
            new PayrollRunRecord
            {
                Id = Guid.NewGuid(),
                OriginalFileName = "old-review.xlsx",
                SourceRelativePath = "runs/review/source.xlsx",
                Status = PayrollRunStatus.NeedsReview,
                CreatedAtUtc = old,
                UpdatedAtUtc = old,
                RuleSetVersion = "test"
            },
            new PayrollRunRecord
            {
                Id = Guid.NewGuid(),
                OriginalFileName = "old-deleted.xlsx",
                SourceRelativePath = string.Empty,
                Status = PayrollRunStatus.Deleted,
                CreatedAtUtc = old,
                UpdatedAtUtc = old,
                RuleSetVersion = "test"
            });
        db.AutomationJobs.AddRange(
            CreateJob(PayrollAutomationJobStatus.Exported, 31),
            CreateJob(PayrollAutomationJobStatus.NeedsReview, 31));
        await db.SaveChangesAsync();

        PayrollAutomationOperationsService service = CreateService(
            db,
            new PayrollStorageOptions { RetentionDays = 30 });
        PayrollRetentionPreview preview = await service.PreviewRetentionAsync(now);

        Assert.Equal(1, preview.EligibleRunCount);
        Assert.Equal(1, preview.EligibleJobCount);
        Assert.Equal(2, preview.ProtectedRunCount);
        Assert.Equal(1, preview.ProtectedJobCount);
        Assert.Equal(now.AddDays(-30), preview.CutoffUtc);
    }

    private static PayrollAutomationOperationsService CreateService(
        PayrollDbContext db,
        PayrollStorageOptions? options = null) =>
        new(
            options ?? new PayrollStorageOptions(),
            new EfAutomationJobRepository(db),
            new EfPayrollRunRepository(db),
            null!);

    private static PayrollAutomationJobRecord CreateJob(
        PayrollAutomationJobStatus status,
        int daysAgo)
    {
        DateTimeOffset updated = new DateTimeOffset(2026, 10, 27, 8, 0, 0, TimeSpan.Zero).AddDays(-daysAgo);
        return new PayrollAutomationJobRecord
        {
            Id = Guid.NewGuid(),
            SourcePath = $"inbox/{Guid.NewGuid():N}.xlsx",
            OriginalFileName = "synthetic.xlsx",
            ContentHash = Guid.NewGuid().ToString("N").PadRight(64, 'a'),
            Trigger = "Test",
            Status = status,
            UpdatedAtUtc = updated,
            DiscoveredAtUtc = updated
        };
    }

    private static async Task<SqliteConnection> OpenDatabaseAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        await db.Database.EnsureCreatedAsync();
        return connection;
    }

    private static PayrollDbContext CreateDatabase(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<PayrollDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new PayrollDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
