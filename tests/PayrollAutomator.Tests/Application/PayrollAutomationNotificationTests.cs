using System.Net;
using PayrollAutomator.Application;
using Xunit;

namespace PayrollAutomator.Tests.Application;

public sealed class PayrollAutomationNotificationTests
{
    [Fact]
    public async Task WebhookSender_OnlySendsOperationalFields()
    {
        var handler = new CaptureHandler();
        using var client = new HttpClient(handler);
        var options = new PayrollStorageOptions
        {
            Automation = new PayrollAutomationOptions
            {
                NotificationEndpoint = "https://example.test/hook",
                NotificationProvider = "Webhook"
            }
        };
        var sender = new WebhookPayrollAutomationNotificationSender(client, options);
        Guid jobId = Guid.NewGuid();

        await sender.SendAsync(new PayrollAutomationNotification(
            jobId,
            null,
            PayrollAutomationResultCode.NeedsReview,
            PayrollAutomationJobStatus.NeedsReview,
            null,
            DateTimeOffset.UtcNow));

        Assert.NotNull(handler.Body);
        Assert.Contains(jobId.ToString(), handler.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payable", handler.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("employee", handler.Body, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }
    }
}
