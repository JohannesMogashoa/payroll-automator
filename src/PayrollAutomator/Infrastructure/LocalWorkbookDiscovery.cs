using System.Security.Cryptography;
using PayrollAutomator.Application;

namespace PayrollAutomator.Infrastructure;

public sealed class LocalWorkbookDiscovery : IWorkbookDiscovery
{
    public async Task<IReadOnlyList<PayrollWorkbookCandidate>> DiscoverAsync(
        string inputDirectory,
        long maximumFileSize,
        TimeSpan stabilityDelay,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputDirectory);
        Directory.CreateDirectory(inputDirectory);
        string[] files = Directory.GetFiles(inputDirectory, "*", SearchOption.TopDirectoryOnly);
        var candidates = new List<PayrollWorkbookCandidate>(files.Length);
        foreach (string file in files.Order(StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add(await InspectAsync(file, maximumFileSize, stabilityDelay, cancellationToken));
        }

        return candidates;
    }

    public async Task<PayrollWorkbookCandidate> InspectAsync(
        string sourcePath,
        long maximumFileSize,
        TimeSpan stabilityDelay,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        string fullPath = Path.GetFullPath(sourcePath);
        FileInfo initial = new(fullPath);
        if (!initial.Exists)
        {
            return Failure(fullPath, "FileNotFound", 0, DateTimeOffset.UtcNow);
        }

        if (!string.Equals(initial.Extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(fullPath, "UnsupportedExtension", initial.Length, initial.LastWriteTimeUtc);
        }

        if (initial.Length > maximumFileSize)
        {
            return Failure(fullPath, "WorkbookTooLarge", initial.Length, initial.LastWriteTimeUtc);
        }

        if (stabilityDelay > TimeSpan.Zero)
        {
            await Task.Delay(stabilityDelay, cancellationToken);
        }

        FileInfo final = new(fullPath);
        bool stable = final.Exists &&
                      final.Length == initial.Length &&
                      final.LastWriteTimeUtc == initial.LastWriteTimeUtc;
        if (!stable)
        {
            return new PayrollWorkbookCandidate(
                fullPath, initial.Name, initial.Length, initial.LastWriteTimeUtc, false, null, null);
        }

        try
        {
            await using FileStream content = new(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            byte[] hash = await SHA256.HashDataAsync(content, cancellationToken);
            return new PayrollWorkbookCandidate(
                fullPath,
                initial.Name,
                initial.Length,
                initial.LastWriteTimeUtc,
                true,
                Convert.ToHexString(hash),
                null);
        }
        catch (IOException)
        {
            return Failure(fullPath, "FileInaccessible", initial.Length, initial.LastWriteTimeUtc);
        }
    }

    private static PayrollWorkbookCandidate Failure(
        string fullPath,
        string code,
        long size,
        DateTimeOffset lastWriteTimeUtc) =>
        new(fullPath, Path.GetFileName(fullPath), size, lastWriteTimeUtc, false, null, code);
}
