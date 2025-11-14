using BizFirstMetaMessenger.Data;

namespace BizFirstMetaMessenger.Services;

/// <summary>
/// Interface for webhook database operations
/// </summary>
public interface IWebhookDatabaseService
{
    /// <summary>
    /// Saves a webhook event and its parsed messages to the database
    /// </summary>
    Task<int> SaveWebhookEventAsync(string eventType, string rawPayload, string? objectType = null);

    /// <summary>
    /// Saves a parsed message to the database
    /// </summary>
    Task<int> SaveMessageAsync(int webhookEventId, Message message);

    /// <summary>
    /// Gets all webhook events with optional filtering
    /// </summary>
    Task<List<WebhookEvent>> GetWebhookEventsAsync(DateTime? startDate = null, DateTime? endDate = null, string? eventType = null);

    /// <summary>
    /// Gets all messages with optional filtering
    /// </summary>
    Task<List<Message>> GetMessagesAsync(string? senderId = null, DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// Gets unresponded messages
    /// </summary>
    Task<List<Message>> GetUnrespondedMessagesAsync();
}
