using Microsoft.EntityFrameworkCore;
using PayrollAutomator.Application;

namespace PayrollAutomator.Infrastructure;

public sealed class PayrollDbContext(DbContextOptions<PayrollDbContext> options)
    : DbContext(options)
{
    public DbSet<PayrollRunRecord> PayrollRuns => Set<PayrollRunRecord>();

    public DbSet<PayrollAuditRecord> PayrollAuditEvents => Set<PayrollAuditRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PayrollRunRecord>(entity =>
        {
            entity.HasKey(run => run.Id);
            entity.Property(run => run.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(run => run.OriginalFileName).IsRequired().HasMaxLength(255);
            entity.Property(run => run.SourceRelativePath).IsRequired().HasMaxLength(500);
            entity.Property(run => run.ExportRelativePath).HasMaxLength(500);
            entity.Property(run => run.RuleSetVersion).IsRequired().HasMaxLength(100);
            entity.HasIndex(run => run.UpdatedAtUtc);
        });

        modelBuilder.Entity<PayrollAuditRecord>(entity =>
        {
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.EventType).IsRequired().HasMaxLength(64);
            entity.Property(audit => audit.Details).IsRequired().HasMaxLength(2000);
            entity.HasIndex(audit => new { audit.RunId, audit.TimestampUtc });
        });
    }
}
