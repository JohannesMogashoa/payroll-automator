using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace PayrollAutomator.Application;

public sealed record PayrollAutomationNotification(
    Guid JobId,
    Guid? PayrollRunId,
    PayrollAutomationResultCode ResultCode,
    PayrollAutomationJobStatus Status,
    string? FailureCode,
    DateTimeOffset OccurredAtUtc);

public interface IPayrollAutomationNotificationSender
{
    Task SendAsync(
        PayrollAutomationNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed class NullPayrollAutomationNotificationSender : IPayrollAutomationNotificationSender
{
    public Task SendAsync(
        PayrollAutomationNotification notification,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public sealed class ConsolePayrollAutomationNotificationSender(
    ILogger<ConsolePayrollAutomationNotificationSender> logger)
    : IPayrollAutomationNotificationSender
{
    public Task SendAsync(
        PayrollAutomationNotification notification,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Payroll automation notification: job {JobId}, run {PayrollRunId}, result {ResultCode}, status {Status}, failure {FailureCode}",
            notification.JobId,
            notification.PayrollRunId,
            notification.ResultCode,
            notification.Status,
            notification.FailureCode ?? "none");
        return Task.CompletedTask;
    }
}

public sealed class WebhookPayrollAutomationNotificationSender(
    HttpClient httpClient,
    PayrollStorageOptions options)
    : IPayrollAutomationNotificationSender
{
    public async Task SendAsync(
        PayrollAutomationNotification notification,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Automation.NotificationEndpoint))
        {
            throw new InvalidOperationException(
                "Payroll:Automation:NotificationEndpoint is required for webhook notifications.");
        }

        var payload = new RedactedWebhookPayload(
            "payroll-automation",
            notification.JobId,
            notification.PayrollRunId,
            notification.ResultCode.ToString(),
            notification.Status.ToString(),
            notification.FailureCode,
            notification.OccurredAtUtc);
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            options.Automation.NotificationEndpoint,
            payload,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private sealed record RedactedWebhookPayload(
        [property: JsonPropertyName("source")] string Source,
        [property: JsonPropertyName("jobId")] Guid JobId,
        [property: JsonPropertyName("payrollRunId")] Guid? PayrollRunId,
        [property: JsonPropertyName("resultCode")] string ResultCode,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("failureCode")] string? FailureCode,
        [property: JsonPropertyName("occurredAtUtc")] DateTimeOffset OccurredAtUtc);
}

public sealed class PayrollAutomationNotificationDispatcher(
    PayrollStorageOptions options,
    IPayrollAutomationNotificationSender sender,
    ILogger<PayrollAutomationNotificationDispatcher> logger)
{
    private readonly HashSet<string> notifyOn = options.Automation.NotifyOn
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public async Task TrySendAsync(
        PayrollAutomationProcessResult result,
        CancellationToken cancellationToken = default)
    {
        if (result.JobId is null || !notifyOn.Contains(result.Code.ToString()))
        {
            return;
        }

        try
        {
            await sender.SendAsync(
                new PayrollAutomationNotification(
                    result.JobId.Value,
                    result.PayrollRunId,
                    result.Code,
                    result.Status,
                    result.FailureCode,
                    DateTimeOffset.UtcNow),
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            logger.LogError(exception, "Payroll automation notification delivery failed for job {JobId}", result.JobId);
        }
    }
}
