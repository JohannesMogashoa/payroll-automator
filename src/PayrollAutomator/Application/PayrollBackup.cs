namespace PayrollAutomator.Application;

public sealed record PayrollBackupResult(
    string BackupDirectory,
    int FileCount,
    string ManifestPath);

public interface IPayrollBackupService
{
    Task<PayrollBackupResult> CreateAsync(CancellationToken cancellationToken = default);

    Task VerifyAsync(
        string backupDirectory,
        CancellationToken cancellationToken = default);
}
