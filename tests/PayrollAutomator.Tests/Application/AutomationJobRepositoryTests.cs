using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PayrollAutomator.Application;
using PayrollAutomator.Infrastructure;
using Xunit;

namespace PayrollAutomator.Tests.Application;

public sealed class AutomationJobRepositoryTests
{
    [Fact]
    public async Task TryClaimAsync_ClaimsQueuedJobAndIncrementsAttempt()
    {
        await using SqliteConnection connection = await OpenDatabaseAsync();
        await using var db = CreateDatabase(connection);
        var repository = new EfAutomationJobRepository(db);
        Guid jobId = Guid.NewGuid();
        await repository.AddAsync(CreateJob(jobId));

        bool claimed = await repository.TryClaimAsync(
            jobId,
            "worker-a",
            UtcNow,
            TimeSpan.FromMinutes(5),
            maxAttempts: 3);

        PayrollAutomationJobRecord job = await repository.GetAsync(jobId)
            ?? throw new InvalidOperationException("Job was not persisted.");
        Assert.True(claimed);
        Assert.Equal(PayrollAutomationJobStatus.Processing, job.Status);
        Assert.Equal(1, job.AttemptCount);
        Assert.Equal("worker-a", job.WorkerId);
        Assert.NotNull(job.LeaseExpiresAtUtc);
    }

    [Fact]
    public async Task TryClaimAsync_AllowsExpiredLeaseToBeReclaimed()
    {
        await using SqliteConnection connection = await OpenDatabaseAsync();
        await using var db = CreateDatabase(connection);
        var repository = new EfAutomationJobRepository(db);
        Guid jobId = Guid.NewGuid();
        PayrollAutomationJobRecord job = CreateJob(jobId);
        job.Status = PayrollAutomationJobStatus.Processing;
        job.AttemptCount = 1;
        job.WorkerId = "dead-worker";
        job.LeaseExpiresAtUtc = UtcNow.AddMinutes(-1);
        await repository.AddAsync(job);

        bool claimed = await repository.TryClaimAsync(
            jobId,
            "worker-b",
            UtcNow,
            TimeSpan.FromMinutes(5),
            maxAttempts: 3);

        PayrollAutomationJobRecord claimedJob = await repository.GetAsync(jobId)
            ?? throw new InvalidOperationException("Job was not persisted.");
        Assert.True(claimed);
        Assert.Equal(2, claimedJob.AttemptCount);
        Assert.Equal("worker-b", claimedJob.WorkerId);
    }

    [Fact]
    public async Task TryClaimAsync_DoesNotClaimBeyondMaximumAttempts()
    {
        await using SqliteConnection connection = await OpenDatabaseAsync();
        await using var db = CreateDatabase(connection);
        var repository = new EfAutomationJobRepository(db);
        Guid jobId = Guid.NewGuid();
        PayrollAutomationJobRecord job = CreateJob(jobId);
        job.AttemptCount = 3;
        await repository.AddAsync(job);

        bool claimed = await repository.TryClaimAsync(
            jobId,
            "worker-a",
            UtcNow,
            TimeSpan.FromMinutes(5),
            maxAttempts: 3);

        Assert.False(claimed);
    }

    [Fact]
    public async Task TryUpdateOutcomeAsync_RequiresCurrentWorkerLease()
    {
        await using SqliteConnection connection = await OpenDatabaseAsync();
        await using var db = CreateDatabase(connection);
        var repository = new EfAutomationJobRepository(db);
        Guid jobId = Guid.NewGuid();
        await repository.AddAsync(CreateJob(jobId));
        Assert.True(await repository.TryClaimAsync(
            jobId,
            "worker-a",
            UtcNow,
            TimeSpan.FromMinutes(5),
            maxAttempts: 3));

        bool wrongWorker = await repository.TryUpdateOutcomeAsync(
            jobId,
            "worker-b",
            PayrollAutomationJobStatus.NeedsReview,
            PayrollAutomationResultCode.NeedsReview,
            Guid.NewGuid(),
            UtcNow);
        bool completed = await repository.TryUpdateOutcomeAsync(
            jobId,
            "worker-a",
            PayrollAutomationJobStatus.NeedsReview,
            PayrollAutomationResultCode.NeedsReview,
            Guid.NewGuid(),
            UtcNow);

        Assert.False(wrongWorker);
        Assert.True(completed);
    }

    [Fact]
    public void StateMachine_RejectsSkippingProcessing()
    {
        Assert.False(PayrollAutomationJobStateMachine.CanTransition(
            PayrollAutomationJobStatus.Queued,
            PayrollAutomationJobStatus.Exported));
        Assert.Throws<InvalidOperationException>(() =>
            PayrollAutomationJobStateMachine.EnsureTransition(
                PayrollAutomationJobStatus.Queued,
                PayrollAutomationJobStatus.Exported));
    }

    [Fact]
    public async Task FindBySourceAsync_UsesPathAndContentHashIdentity()
    {
        await using SqliteConnection connection = await OpenDatabaseAsync();
        await using var db = CreateDatabase(connection);
        var repository = new EfAutomationJobRepository(db);
        PayrollAutomationJobRecord original = CreateJob(Guid.NewGuid());
        await repository.AddAsync(original);

        PayrollAutomationJobRecord? found = await repository.FindBySourceAsync(
            original.SourcePath,
            original.ContentHash);
        PayrollAutomationJobRecord? differentHash = await repository.FindBySourceAsync(
            original.SourcePath,
            "different-hash");

        Assert.NotNull(found);
        Assert.Equal(original.Id, found.Id);
        Assert.Null(differentHash);
    }

    private static PayrollAutomationJobRecord CreateJob(Guid id) =>
        new()
        {
            Id = id,
            SourcePath = "inbox/synthetic-payroll.xlsx",
            OriginalFileName = "synthetic-payroll.xlsx",
            ContentHash = "a".PadRight(64, 'a'),
            Trigger = "Manual",
            Status = PayrollAutomationJobStatus.Queued,
            DiscoveredAtUtc = UtcNow,
            UpdatedAtUtc = UtcNow
        };

    private static DateTimeOffset UtcNow =>
        new(2026, 10, 27, 8, 0, 0, TimeSpan.Zero);

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
