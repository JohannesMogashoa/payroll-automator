using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PayrollAutomator.Application;
using PayrollAutomator.Components.Pages;
using Xunit;

namespace PayrollAutomator.Tests.Components;

public sealed class PayrollWorkflowComponentTests : BunitContext
{
    [Fact]
    public void Home_RendersEmptyStateAndUploadLink()
    {
        Services.Add(ServiceDescriptor.Singleton<IPayrollRunService>(new FakePayrollRunService()));

        var cut = Render<Home>();

        Assert.Contains("No runs yet", cut.Markup);
        Assert.Contains("/runs/new", cut.Markup);
    }

    [Fact]
    public void NewRun_RendersUploadContractAndConfiguredLimit()
    {
        Services.Add(ServiceDescriptor.Singleton<IPayrollRunService>(new FakePayrollRunService()));
        Services.Add(ServiceDescriptor.Singleton<IOptions<PayrollStorageOptions>>(
            Options.Create(new PayrollStorageOptions { MaxUploadBytes = 5 * 1024 * 1024 })));

        var cut = Render<NewRun>();

        Assert.Contains("Upload workbook", cut.Markup);
        Assert.Contains("5 MB", cut.Markup);
        Assert.DoesNotContain("Import and review", cut.Markup);
    }

    [Fact]
    public void RunDetails_RequiresSecondClickBeforeDeleting()
    {
        Guid runId = Guid.NewGuid();
        var fake = new FakePayrollRunService(CreateDetails(runId));
        Services.Add(ServiceDescriptor.Singleton<IPayrollRunService>(fake));

        var cut = Render<RunDetails>(parameters => parameters.Add(component => component.Id, runId));

        cut.FindAll("button").Single(button => button.TextContent.Contains("Delete run")).Click();
        Assert.Contains("Click again to confirm deletion", cut.Markup);
        Assert.Equal(0, fake.DeleteCalls);

        cut.FindAll("button").Single(button => button.TextContent.Contains("Confirm deletion")).Click();
        Assert.Equal(1, fake.DeleteCalls);
    }

    [Fact]
    public void RunDetails_ConfirmsReviewThroughInjectedService()
    {
        Guid runId = Guid.NewGuid();
        var fake = new FakePayrollRunService(CreateDetails(runId));
        Services.Add(ServiceDescriptor.Singleton<IPayrollRunService>(fake));

        var cut = Render<RunDetails>(parameters => parameters.Add(component => component.Id, runId));
        cut.Find("#reviewer").Change("Synthetic Reviewer");
        cut.Find("button.button.primary").Click();

        Assert.Equal("Synthetic Reviewer", fake.LastReviewer);
        Assert.Equal(runId, fake.LastConfirmedRunId);
    }

    private static PayrollRunDetails CreateDetails(Guid runId)
    {
        var summary = new PayrollRunSummary(
            runId,
            "synthetic.xlsx",
            PayrollRunStatus.NeedsReview,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            0,
            0,
            1m,
            195m,
            false);
        var review = new PayrollReviewSummary([], 1, 1, 0, 0, 1m, 195m);
        var calculation = new PayrollCalculationResult(
            [],
            [],
            [],
            [],
            new PayrollCalculationMetadata("test", 195m, new Dictionary<PayrollAutomator.Domain.PayrollCategory, decimal>()));
        return new PayrollRunDetails(summary, calculation, review, [], null, null, null);
    }

    private sealed class FakePayrollRunService(PayrollRunDetails? details = null) : IPayrollRunService
    {
        private readonly PayrollRunDetails? runDetails = details;

        public int DeleteCalls { get; private set; }

        public Guid? LastConfirmedRunId { get; private set; }

        public string? LastReviewer { get; private set; }

        public Task<PayrollRunSummary> CreateRunAsync(
            Stream workbook,
            string originalFileName,
            PayrollAutomator.Domain.PayrollRatePolicy? ratePolicy = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(runDetails!.Summary);

        public Task<IReadOnlyList<PayrollRunSummary>> ListRunsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PayrollRunSummary>>(
                runDetails is null ? [] : [runDetails.Summary]);

        public Task<PayrollRunDetails?> GetRunAsync(
            Guid runId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(runDetails);

        public Task<PayrollExportResult> ConfirmAndExportAsync(
            Guid runId,
            string reviewer,
            string? comment = null,
            CancellationToken cancellationToken = default)
        {
            LastConfirmedRunId = runId;
            LastReviewer = reviewer;
            return Task.FromResult(new PayrollExportResult("synthetic.xlsx", []));
        }

        public Task<PayrollDownload?> OpenExportAsync(
            Guid runId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PayrollDownload?>(null);

        public Task DeleteRunAsync(
            Guid runId,
            CancellationToken cancellationToken = default,
            string auditEventType = "RunDeleted",
            string auditDetails = "Run artifacts and calculation snapshot were deleted by the operator.")
        {
            DeleteCalls++;
            return Task.CompletedTask;
        }
    }
}
