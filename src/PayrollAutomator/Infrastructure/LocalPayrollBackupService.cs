using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using PayrollAutomator.Application;

namespace PayrollAutomator.Infrastructure;

public sealed class LocalPayrollBackupService(
    PayrollStorageOptions options,
    ILogger<LocalPayrollBackupService>? logger = null) : IPayrollBackupService
{
    private const string ManifestFileName = "manifest.json";

    public async Task<PayrollBackupResult> CreateAsync(CancellationToken cancellationToken = default)
    {
        string backupRoot = PayrollAutomationConfiguration.ResolvePath(options, options.Paths.Backup);
        Directory.CreateDirectory(backupRoot);
        string finalDirectory = Path.Combine(
            backupRoot,
            DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));
        string temporaryDirectory = finalDirectory + $".tmp-{Guid.NewGuid():N}";
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            string sourceDatabase = Path.GetFullPath(Path.Combine(options.DataRoot, "payroll.db"));
            string backupDatabase = Path.Combine(temporaryDirectory, "payroll.db");
            await using (var source = new SqliteConnection($"Data Source={sourceDatabase};Pooling=False"))
            await using (var destination = new SqliteConnection($"Data Source={backupDatabase};Pooling=False"))
            {
                await source.OpenAsync(cancellationToken);
                await destination.OpenAsync(cancellationToken);
                source.BackupDatabase(destination);
            }

            string sourceRuns = Path.GetFullPath(Path.Combine(options.DataRoot, "runs"));
            if (Directory.Exists(sourceRuns))
            {
                CopyDirectory(sourceRuns, Path.Combine(temporaryDirectory, "runs"));
            }

            List<PayrollBackupManifestEntry> entries = [];
            foreach (string path in Directory.EnumerateFiles(temporaryDirectory, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relativePath = Path.GetRelativePath(temporaryDirectory, path);
                entries.Add(new PayrollBackupManifestEntry(
                    relativePath,
                    new FileInfo(path).Length,
                    await HashFileAsync(path, cancellationToken)));
            }

            string manifestPath = Path.Combine(temporaryDirectory, ManifestFileName);
            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(new PayrollBackupManifest(DateTimeOffset.UtcNow, entries)),
                cancellationToken);
            if (Directory.Exists(finalDirectory))
            {
                throw new IOException($"Backup destination already exists: {finalDirectory}");
            }

            Directory.Move(temporaryDirectory, finalDirectory);
            PayrollBackupResult result = new(
                finalDirectory,
                entries.Count,
                Path.Combine(finalDirectory, ManifestFileName));
            logger?.LogInformation(
                "Payroll backup created at {BackupDirectory} with {FileCount} manifest file(s)",
                result.BackupDirectory,
                result.FileCount);
            return result;
        }
        catch
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }

            throw;
        }
    }

    public async Task VerifyAsync(
        string backupDirectory,
        CancellationToken cancellationToken = default)
    {
        string fullDirectory = Path.GetFullPath(backupDirectory);
        string manifestPath = Path.Combine(fullDirectory, ManifestFileName);
        PayrollBackupManifest manifest = JsonSerializer.Deserialize<PayrollBackupManifest>(
            await File.ReadAllTextAsync(manifestPath, cancellationToken))
            ?? throw new InvalidDataException("Backup manifest is invalid.");
        foreach (PayrollBackupManifestEntry entry in manifest.Files)
        {
            string path = Path.GetFullPath(Path.Combine(fullDirectory, entry.RelativePath));
            if (!path.StartsWith(fullDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(path))
            {
                throw new InvalidDataException($"Backup file is missing: {entry.RelativePath}");
            }

            string hash = await HashFileAsync(path, cancellationToken);
            if (!string.Equals(hash, entry.Sha256, StringComparison.OrdinalIgnoreCase) ||
                new FileInfo(path).Length != entry.Length)
            {
                throw new InvalidDataException($"Backup checksum failed: {entry.RelativePath}");
            }
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string destinationFile = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(file, destinationFile);
        }
    }

    private static async Task<string> HashFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private sealed record PayrollBackupManifest(
        DateTimeOffset CreatedAtUtc,
        IReadOnlyList<PayrollBackupManifestEntry> Files);

    private sealed record PayrollBackupManifestEntry(
        string RelativePath,
        long Length,
        string Sha256);
}
