using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PayrollAutomator.Application;

namespace PayrollAutomator.Infrastructure;

public sealed class PayrollAutomationWorker(
    IServiceScopeFactory scopeFactory,
    PayrollStorageOptions options,
    ILogger<PayrollAutomationWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Automation.Enabled)
        {
            logger.LogInformation("Payroll automation worker is disabled by configuration.");
            return;
        }

        foreach (string relativePath in new[]
        {
            options.Paths.Root,
            options.Paths.Inbox,
            options.Paths.Processing,
            options.Paths.Processed,
            options.Paths.Quarantine,
            options.Paths.Output,
            options.Paths.Backup
        })
        {
            Directory.CreateDirectory(PayrollAutomationConfiguration.ResolvePath(options, relativePath));
        }

        using var concurrencyGate = new SemaphoreSlim(options.Automation.MaxConcurrentJobs);
        using var timer = new PeriodicTimer(options.Automation.ScanInterval);
        do
        {
            await RunScanAsync(concurrencyGate, stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunScanAsync(
        SemaphoreSlim concurrencyGate,
        CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            IWorkbookDiscovery discovery = scope.ServiceProvider.GetRequiredService<IWorkbookDiscovery>();
            string inbox = PayrollAutomationConfiguration.ResolvePath(options, options.Paths.Inbox);
            IReadOnlyList<PayrollWorkbookCandidate> candidates = await discovery.DiscoverAsync(
                inbox,
                options.MaxUploadBytes,
                options.Automation.FileStabilityDelay,
                cancellationToken);
            var results = new List<PayrollAutomationProcessResult>();
            var resultsLock = new object();
            var tasks = candidates.Select(async candidate =>
            {
                await concurrencyGate.WaitAsync(cancellationToken);
                try
                {
                    await using AsyncServiceScope candidateScope = scopeFactory.CreateAsyncScope();
                    PayrollAutomationProcessor processor = candidateScope.ServiceProvider
                        .GetRequiredService<PayrollAutomationProcessor>();
                    PayrollAutomationProcessResult result = await processor.ProcessFileAsync(
                        candidate.SourcePath,
                        "ScheduledScan",
                        quarantineInvalidFile: true,
                        cancellationToken);
                    lock (resultsLock)
                    {
                        results.Add(result);
                    }
                }
                finally
                {
                    concurrencyGate.Release();
                }
            });
            await Task.WhenAll(tasks);
            PayrollAutomationScanSummary summary = Summarize(candidates.Count, results);
            logger.LogInformation(
                "Payroll automation scan completed: candidates {Candidates}, processed {Processed}, review {NeedsReview}, failed {Failed}",
                summary.CandidateCount,
                summary.ProcessedCount,
                summary.NeedsReviewCount,
                summary.FailedCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Payroll automation worker is shutting down.");
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            logger.LogError(exception, "Payroll automation scheduled scan failed; the next interval will retry.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Payroll automation scheduled scan encountered an unexpected error; the next interval will retry.");
        }
    }

    private static PayrollAutomationScanSummary Summarize(
        int candidateCount,
        IReadOnlyList<PayrollAutomationProcessResult> results) =>
        new(
            candidateCount,
            results.Count(result => result.Code is PayrollAutomationResultCode.Completed or PayrollAutomationResultCode.NeedsReview),
            results.Count(result => result.Code == PayrollAutomationResultCode.NeedsReview),
            results.Count(result => result.Code == PayrollAutomationResultCode.SkippedDuplicate),
            results.Count(result => result.Code == PayrollAutomationResultCode.WaitingForStability),
            results.Count(result => result.Code == PayrollAutomationResultCode.Quarantined),
            results.Count(result => result.Code == PayrollAutomationResultCode.Failed));
}
