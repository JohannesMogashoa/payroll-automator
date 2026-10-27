using Microsoft.EntityFrameworkCore;
using PayrollAutomator.Application;

namespace PayrollAutomator.Infrastructure;

public sealed class EfPayrollRunRepository(PayrollDbContext db) : IPayrollRunRepository
{
    public async Task AddAsync(
        PayrollRunRecord run,
        PayrollAuditRecord audit,
        CancellationToken cancellationToken = default)
    {
        db.PayrollRuns.Add(run);
        db.PayrollAuditEvents.Add(audit);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        PayrollRunRecord run,
        PayrollAuditRecord audit,
        CancellationToken cancellationToken = default)
    {
        PayrollRunRecord? tracked = db.PayrollRuns.Local
            .SingleOrDefault(existing => existing.Id == run.Id);
        if (tracked is null)
        {
            db.PayrollRuns.Attach(run);
        }
        else if (!ReferenceEquals(tracked, run))
        {
            db.Entry(tracked).CurrentValues.SetValues(run);
        }

        db.PayrollAuditEvents.Add(audit);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<PayrollRunRecord?> GetAsync(
        Guid runId,
        CancellationToken cancellationToken = default) =>
        db.PayrollRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(run => run.Id == runId, cancellationToken);

    public async Task<PayrollRunRecord> GetRequiredAsync(
        Guid runId,
        CancellationToken cancellationToken = default) =>
        await GetAsync(runId, cancellationToken)
        ?? throw new KeyNotFoundException($"Payroll run '{runId}' was not found.");

    public async Task<IReadOnlyList<PayrollRunRecord>> ListAsync(
        CancellationToken cancellationToken = default) =>
        (await db.PayrollRuns
            .AsNoTracking()
            .ToListAsync(cancellationToken))
        .OrderByDescending(run => run.UpdatedAtUtc)
        .ToArray();

    public async Task<IReadOnlyList<PayrollAuditRecord>> ListAuditAsync(
        Guid runId,
        CancellationToken cancellationToken = default) =>
        (await db.PayrollAuditEvents
            .AsNoTracking()
            .Where(audit => audit.RunId == runId)
            .ToListAsync(cancellationToken))
        .OrderBy(audit => audit.TimestampUtc)
        .ToArray();
}
