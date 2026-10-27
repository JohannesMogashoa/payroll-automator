using PayrollAutomator.Application;
using PayrollAutomator.Infrastructure;
using Xunit;

namespace PayrollAutomator.Tests.Infrastructure;

public sealed class LocalWorkbookDiscoveryTests
{
    [Fact]
    public async Task InspectAsync_ReturnsStableHashForWorkbook()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "synthetic-payroll.xlsx");
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4]);

        PayrollWorkbookCandidate candidate = await new LocalWorkbookDiscovery().InspectAsync(
            path,
            maximumFileSize: 100,
            stabilityDelay: TimeSpan.Zero);

        Assert.True(candidate.IsStable);
        Assert.Equal(64, candidate.ContentHash?.Length);
        Assert.Null(candidate.FailureCode);
        DeleteTemporaryDirectory(directory);
    }

    [Fact]
    public async Task InspectAsync_RejectsUnsupportedExtension()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "not-a-workbook.csv");
        await File.WriteAllTextAsync(path, "synthetic");

        PayrollWorkbookCandidate candidate = await new LocalWorkbookDiscovery().InspectAsync(
            path,
            maximumFileSize: 100,
            stabilityDelay: TimeSpan.Zero);

        Assert.Equal("UnsupportedExtension", candidate.FailureCode);
        Assert.False(candidate.IsStable);
        DeleteTemporaryDirectory(directory);
    }

    [Fact]
    public async Task InspectAsync_RejectsOversizedWorkbook()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "oversized.xlsx");
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4]);

        PayrollWorkbookCandidate candidate = await new LocalWorkbookDiscovery().InspectAsync(
            path,
            maximumFileSize: 3,
            stabilityDelay: TimeSpan.Zero);

        Assert.Equal("WorkbookTooLarge", candidate.FailureCode);
        DeleteTemporaryDirectory(directory);
    }

    [Fact]
    public async Task InspectAsync_ReportsMissingFile()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "missing.xlsx");

        PayrollWorkbookCandidate candidate = await new LocalWorkbookDiscovery().InspectAsync(
            path,
            maximumFileSize: 100,
            stabilityDelay: TimeSpan.Zero);

        Assert.Equal("FileNotFound", candidate.FailureCode);
        DeleteTemporaryDirectory(directory);
    }

    [Fact]
    public async Task DiscoverAsync_ReturnsCandidatesInStableOrder()
    {
        string directory = CreateTemporaryDirectory();
        await File.WriteAllBytesAsync(Path.Combine(directory, "b.xlsx"), [2]);
        await File.WriteAllBytesAsync(Path.Combine(directory, "a.xlsx"), [1]);

        IReadOnlyList<PayrollWorkbookCandidate> candidates = await new LocalWorkbookDiscovery().DiscoverAsync(
            directory,
            maximumFileSize: 100,
            stabilityDelay: TimeSpan.Zero);

        Assert.Equal(["a.xlsx", "b.xlsx"], candidates.Select(candidate => candidate.OriginalFileName));
        DeleteTemporaryDirectory(directory);
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "payroll-automator-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteTemporaryDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
