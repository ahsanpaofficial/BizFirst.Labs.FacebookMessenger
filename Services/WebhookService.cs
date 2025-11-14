using System.Text.Json;
using BizFirstMetaMessenger.Models;
using BizFirstMetaMessenger.Data;

namespace BizFirstMetaMessenger.Services;

/// <summary>
/// Service for processing webhook events from Meta
/// </summary>
public class WebhookService : IWebhookService
{
    private readonly ILogger<WebhookService> _logger;
    private readonly IWebhookStorageService _storageService;
    private readonly IWebhookDatabaseService _databaseService;

    public WebhookService(ILogger<WebhookService> logger, IWebhookStorageService storageService, IWebhookDatabaseService databaseService)
    {
        _logger = logger;
        _storageService = storageService;
        _databaseService = databaseService;
    }

    /// <summary>
    /// Processes incoming webhook events from Meta
    /// </summary>
    public async Task ProcessWebhookEventAsync(WebhookEventRequest webhookEvent)
    {
        _logger.LogInformation("Processing webhook - Object: {Object}", webhookEvent.Object);

        // Save raw webhook to JSON file (for backup/debugging)
        if (!string.IsNullOrEmpty(webhookEvent.RawBody))
        {
            await _storageService.SaveWebhookEventAsync("webhook", webhookEvent.RawBody);
        }

        // Save raw webhook to database
        int webhookEventId = 0;
        if (!string.IsNullOrEmpty(webhookEvent.RawBody))
        {
            webhookEventId = await _databaseService.SaveWebhookEventAsync("webhook", webhookEvent.RawBody, webhookEvent.Object);
        }

        if (webhookEvent.Entry == null || webhookEvent.Entry.Length == 0)
        {
            _logger.LogWarning("No entries in webhook event");
            return;
        }

        _logger.LogInformation("Processing {Count} entries", webhookEvent.Entry.Length);

        foreach (var entry in webhookEvent.Entry)
        {
            await ProcessEntryAsync(entry, webhookEvent.RawBody, webhookEventId);
        }

        await Task.CompletedTask;
    }

    private async Task ProcessEntryAsync(JsonElement entry, string? rawBody, int webhookEventId)
    {
        _logger.LogInformation("Processing entry");

        // Check if this is a field change event (e.g., about, name, etc.)
        if (entry.TryGetProperty("changes", out var changes))
        {
            await ProcessFieldChangesAsync(changes);
            return;
        }

        // Check if this is a messaging event
        if (entry.TryGetProperty("messaging", out var messaging))
        {
            await ProcessMessagingEventsAsync(messaging, webhookEventId);
            return;
        }

        // Log unknown event type
        _logger.LogWarning("Unknown event type in entry: {Entry}", entry.ToString());
    }

    private async Task ProcessFieldChangesAsync(JsonElement changes)
    {
        _logger.LogInformation("=== Field Change Event ===");

        foreach (var change in changes.EnumerateArray())
        {
            var field = change.TryGetProperty("field", out var fieldProp) ? fieldProp.GetString() : "unknown";
            var value = change.TryGetProperty("value", out var valueProp) ? valueProp.GetString() : "unknown";

            _logger.LogInformation("Field '{Field}' updated to: '{Value}'", field, value);

            // Save field change to JSON file
            await _storageService.SaveWebhookEventAsync($"field_{field}", change.ToString());

            // Handle specific fields
            switch (field)
            {
                case "about":
                    _logger.LogInformation("✓ About field updated");
                    // TODO: Store in database, trigger notifications, etc.
                    break;

                case "name":
                    _logger.LogInformation("✓ Name field updated");
                    break;

                case "picture":
                    _logger.LogInformation("✓ Picture field updated");
                    break;

                default:
                    _logger.LogInformation("✓ Field '{Field}' updated", field);
                    break;
            }
        }

        await Task.CompletedTask;
    }

    private async Task ProcessMessagingEventsAsync(JsonElement messaging, int webhookEventId)
    {
        _logger.LogInformation("=== Messaging Event ===");

        foreach (var message in messaging.EnumerateArray())
        {
            // Save messaging event to JSON file (for backup/debugging)
            await _storageService.SaveWebhookEventAsync("messaging", message.ToString());

            // Extract common fields
            var senderId = message.TryGetProperty("sender", out var sender) && sender.TryGetProperty("id", out var senderIdProp)
                ? senderIdProp.GetString() ?? "unknown"
                : "unknown";

            var recipientId = message.TryGetProperty("recipient", out var recipient) && recipient.TryGetProperty("id", out var recipientIdProp)
                ? recipientIdProp.GetString() ?? "unknown"
                : "unknown";

            var timestamp = message.TryGetProperty("timestamp", out var timestampProp)
                ? DateTimeOffset.FromUnixTimeMilliseconds(timestampProp.GetInt64()).DateTime
                : DateTime.UtcNow;

            // Check for message
            if (message.TryGetProperty("message", out var messageData))
            {
                var text = messageData.TryGetProperty("text", out var textProp) ? textProp.GetString() : null;
                var mid = messageData.TryGetProperty("mid", out var midProp) ? midProp.GetString() : null;
                var isEcho = messageData.TryGetProperty("is_echo", out var isEchoProp) && isEchoProp.GetBoolean();
                var appId = messageData.TryGetProperty("app_id", out var appIdProp) ? appIdProp.GetInt64().ToString() : null;

                _logger.LogInformation("✓ Message received from {SenderId}: {Text}", senderId, text ?? "no text");

                // Save to database
                var dbMessage = new Message
                {
                    MessageId = mid,
                    SenderId = senderId,
                    RecipientId = recipientId,
                    Text = text,
                    Timestamp = timestamp,
                    MessageType = "message",
                    IsEcho = isEcho,
                    AppId = appId
                };

                await _databaseService.SaveMessageAsync(webhookEventId, dbMessage);
            }

            // Check for postback
            if (message.TryGetProperty("postback", out var postback))
            {
                var payload = postback.TryGetProperty("payload", out var payloadProp) ? payloadProp.GetString() : null;
                _logger.LogInformation("✓ Postback received: {Payload}", payload ?? "no payload");

                // Save to database
                var dbMessage = new Message
                {
                    SenderId = senderId,
                    RecipientId = recipientId,
                    Timestamp = timestamp,
                    MessageType = "postback",
                    PostbackPayload = payload
                };

                await _databaseService.SaveMessageAsync(webhookEventId, dbMessage);
            }

            // Check for delivery
            if (message.TryGetProperty("delivery", out var delivery))
            {
                var watermark = delivery.TryGetProperty("watermark", out var watermarkProp) ? watermarkProp.GetInt64() : (long?)null;
                _logger.LogInformation("✓ Message delivery confirmation");

                // Save to database
                var dbMessage = new Message
                {
                    SenderId = senderId,
                    RecipientId = recipientId,
                    Timestamp = timestamp,
                    MessageType = "delivery",
                    DeliveryWatermark = watermark
                };

                await _databaseService.SaveMessageAsync(webhookEventId, dbMessage);
            }

            // Check for read
            if (message.TryGetProperty("read", out var read))
            {
                _logger.LogInformation("✓ Message read confirmation");

                // Save to database
                var dbMessage = new Message
                {
                    SenderId = senderId,
                    RecipientId = recipientId,
                    Timestamp = timestamp,
                    MessageType = "read"
                };

                await _databaseService.SaveMessageAsync(webhookEventId, dbMessage);
            }
        }

        await Task.CompletedTask;
    }
}
