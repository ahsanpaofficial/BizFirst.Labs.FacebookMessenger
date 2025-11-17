namespace BizFirstMetaMessenger.Services;

/// <summary>
/// Interface for storing webhook events
/// </summary>
public interface IWebhookStorageService
{
    /// <summary>
    /// Saves a webhook event to a JSON file
    /// </summary>
    Task SaveWebhookEventAsync(string eventType, string jsonPayload);
}
