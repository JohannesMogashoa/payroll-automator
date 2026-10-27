using PayrollAutomator.Application;
using Xunit;

namespace PayrollAutomator.Tests.Application;

public sealed class PayrollAutomationConfigurationTests
{
    [Fact]
    public void Defaults_AreDisabledAndSafe()
    {
        var options = new PayrollStorageOptions();

        Assert.False(options.Automation.Enabled);
        Assert.False(options.Automation.AutoExportEnabled);
        Assert.Equal(1, options.Automation.MaxConcurrentJobs);
        Assert.Equal(3, options.Automation.MaxAttempts);
        Assert.Empty(PayrollAutomationConfiguration.Validate(options));
    }

    [Fact]
    public void Validate_RequiresReviewerForAutomaticExport()
    {
        var options = new PayrollStorageOptions
        {
            Automation = new PayrollAutomationOptions
            {
                AutoExportEnabled = true
            }
        };

        IReadOnlyList<string> errors = PayrollAutomationConfiguration.Validate(options);

        Assert.Contains(errors, error => error.Contains("ServiceReviewer", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsRootedAndParentPaths()
    {
        var options = new PayrollStorageOptions
        {
            Paths = new PayrollAutomationPathOptions
            {
                Inbox = "..\\outside"
            }
        };

        IReadOnlyList<string> errors = PayrollAutomationConfiguration.Validate(options);

        Assert.Contains(errors, error => error.Contains("Inbox", StringComparison.Ordinal));
    }

    [Fact]
    public void ResolvePath_StaysWithinAutomationRoot()
    {
        var options = new PayrollStorageOptions
        {
            DataRoot = Path.Combine(Path.GetTempPath(), "payroll-automator-tests", Guid.NewGuid().ToString("N"))
        };

        string resolved = PayrollAutomationConfiguration.ResolvePath(options, "inbox\\synthetic.xlsx");
        string expectedRoot = Path.GetFullPath(Path.Combine(options.DataRoot, options.Paths.Root));

        Assert.StartsWith(expectedRoot + Path.DirectorySeparatorChar, resolved, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolvePath_RejectsTraversal()
    {
        var options = new PayrollStorageOptions();

        Assert.Throws<InvalidOperationException>(() =>
            PayrollAutomationConfiguration.ResolvePath(options, "..\\outside.xlsx"));
    }

    [Fact]
    public void ResolvePath_RejectsUnixTraversal()
    {
        var options = new PayrollStorageOptions();

        Assert.Throws<InvalidOperationException>(() =>
            PayrollAutomationConfiguration.ResolvePath(options, "../outside.xlsx"));
    }
}
