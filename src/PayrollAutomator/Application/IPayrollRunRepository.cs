namespace PayrollAutomator.Application;

public interface IPayrollRunRepository
{
    Task AddAsync(
        PayrollRunRecord run,
        PayrollAuditRecord audit,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        PayrollRunRecord run,
        PayrollAuditRecord audit,
        CancellationToken cancellationToken = default);

    Task<PayrollRunRecord?> GetAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<PayrollRunRecord> GetRequiredAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PayrollRunRecord>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PayrollAuditRecord>> ListAuditAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}

public sealed class PayrollRunRecord
{
    public Guid Id { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string SourceRelativePath { get; set; } = string.Empty;

    public string? ExportRelativePath { get; set; }

    public PayrollRunStatus Status { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? ConfirmedAtUtc { get; set; }

    public DateTimeOffset? ExportedAtUtc { get; set; }

    public string? Reviewer { get; set; }

    public int ErrorCount { get; set; }

    public int WarningCount { get; set; }

    public decimal IncludedPayrollHours { get; set; }

    public decimal IncludedPayableAmount { get; set; }

    public string RuleSetVersion { get; set; } = string.Empty;

    public string? SnapshotJson { get; set; }
}

public sealed class PayrollAuditRecord
{
    public long Id { get; set; }

    public Guid RunId { get; set; }

    public DateTimeOffset TimestampUtc { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;
}
