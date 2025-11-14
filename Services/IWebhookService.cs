using BizFirstMetaMessenger.Models;

namespace BizFirstMetaMessenger.Services;

/// <summary>
/// Interface for webhook processing logic
/// </summary>
public interface IWebhookService
{
    /// <summary>
    /// Processes incoming webhook events from Meta
    /// </summary>
    /// <param name="webhookEvent">The webhook event data</param>
    /// <returns>Task representing the async operation</returns>
    Task ProcessWebhookEventAsync(WebhookEventRequest webhookEvent);
}
