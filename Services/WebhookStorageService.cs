using System.Text.Json;

namespace BizFirstMetaMessenger.Services;

/// <summary>
/// Service for storing webhook events to JSON files
/// </summary>
public class WebhookStorageService : IWebhookStorageService
{
    private readonly ILogger<WebhookStorageService> _logger;
    private readonly string _storageDirectory;

    public WebhookStorageService(ILogger<WebhookStorageService> logger, IConfiguration configuration)
    {
        _logger = logger;

        // Get storage directory from configuration or use default
        _storageDirectory = configuration["WebhookStorage:Directory"] ?? "WebhookData";

        // Create directory if it doesn't exist
        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
            _logger.LogInformation("Created webhook storage directory: {Directory}", _storageDirectory);
        }
    }

    /// <summary>
    /// Saves a webhook event to a JSON file
    /// </summary>
    public async Task SaveWebhookEventAsync(string eventType, string jsonPayload)
    {
        try
        {
            // Create filename with timestamp
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var fileName = $"{eventType}_{timestamp}.json";
            var filePath = Path.Combine(_storageDirectory, fileName);

            // Create wrapper object with metadata
            var webhookData = new
            {
                EventType = eventType,
                ReceivedAt = DateTime.UtcNow,
                Payload = JsonDocument.Parse(jsonPayload).RootElement
            };

            // Save to file
            var options = new JsonSerializerOptions
            {
                WriteIndented = true // Pretty print for readability
            };

            var json = JsonSerializer.Serialize(webhookData, options);
            await File.WriteAllTextAsync(filePath, json);

            _logger.LogInformation("✓ Webhook event saved to: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving webhook event to file");
        }
    }
}
