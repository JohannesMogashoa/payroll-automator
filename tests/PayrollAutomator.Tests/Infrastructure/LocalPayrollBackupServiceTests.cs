using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using PayrollAutomator.Application;
using PayrollAutomator.Infrastructure;
using Xunit;

namespace PayrollAutomator.Tests.Infrastructure;

public sealed class LocalPayrollBackupServiceTests
{
    [Fact]
    public async Task CreateAndVerifyAsync_CreatesAtomicBackupWithManifest()
    {
        string dataRoot = Path.Combine(Path.GetTempPath(), $"payroll-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataRoot);
        try
        {
            var options = new PayrollStorageOptions { DataRoot = dataRoot };
            var dbOptions = new DbContextOptionsBuilder<PayrollDbContext>()
                .UseSqlite($"Data Source={Path.Combine(dataRoot, "payroll.db")}")
                .Options;
            await using (var db = new PayrollDbContext(dbOptions))
            {
                await db.Database.EnsureCreatedAsync();
            }

            string sourceDirectory = Path.Combine(dataRoot, "runs", "synthetic");
            Directory.CreateDirectory(sourceDirectory);
            await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "source.txt"), "synthetic payroll fixture");

            var service = new LocalPayrollBackupService(options);
            PayrollBackupResult result = await service.CreateAsync();
            await service.VerifyAsync(result.BackupDirectory);

            Assert.True(File.Exists(Path.Combine(result.BackupDirectory, "payroll.db")));
            Assert.True(File.Exists(result.ManifestPath));
            Assert.Equal(2, result.FileCount);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(dataRoot))
            {
                Directory.Delete(dataRoot, recursive: true);
            }
        }
    }
}
