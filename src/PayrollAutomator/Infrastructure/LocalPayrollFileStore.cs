using PayrollAutomator.Application;

namespace PayrollAutomator.Infrastructure;

public sealed class LocalPayrollFileStore(PayrollStorageOptions options) : IPayrollFileStore
{
    public async Task<string> SaveSourceAsync(
        Guid runId,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        string safeFileName = Path.GetFileName(originalFileName);
        string relativePath = Path.Combine("runs", runId.ToString("N"), "source", safeFileName);
        string absolutePath = GetAbsolutePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using FileStream target = new(
            absolutePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        await content.CopyToAsync(target, cancellationToken);
        return relativePath;
    }

    public string GetAbsolutePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        string root = Path.GetFullPath(options.DataRoot);
        string absolutePath = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!absolutePath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(absolutePath, root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The requested file path escapes the configured data root.");
        }

        return absolutePath;
    }

    public string GetExportRelativePath(Guid runId) =>
        Path.Combine("runs", runId.ToString("N"), "export", "payroll-export.xlsx");

    public Task DeleteRunArtifactsAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string runDirectory = GetAbsolutePath(Path.Combine("runs", runId.ToString("N")));
        if (Directory.Exists(runDirectory))
        {
            Directory.Delete(runDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }

    public async Task<PayrollDownload?> OpenExportAsync(
        Guid runId,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string relativePath = Path.Combine("runs", runId.ToString("N"), "export", fileName);
        string absolutePath = GetAbsolutePath(relativePath);
        if (!File.Exists(absolutePath))
        {
            return null;
        }

        Stream content = new FileStream(
            absolutePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        await Task.CompletedTask;
        return new PayrollDownload(content, fileName);
    }
}
