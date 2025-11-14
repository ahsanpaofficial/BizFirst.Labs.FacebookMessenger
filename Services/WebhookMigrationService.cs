using System.Text.Json;
using BizFirstMetaMessenger.Data;
using Microsoft.EntityFrameworkCore;

namespace BizFirstMetaMessenger.Services;

/// <summary>
/// Service for migrating existing JSON webhook files to the database
/// </summary>
public class WebhookMigrationService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<WebhookMigrationService> _logger;
    private readonly string _jsonDirectory;

    private readonly string _migrationsDirectory = "Migrations";

    public WebhookMigrationService(IServiceProvider services, ILogger<WebhookMigrationService> logger, IConfiguration configuration)
    {
        _services = services;
        _logger = logger;
        _jsonDirectory = configuration["WebhookStorage:Directory"] ?? "WebhookData";

        // Ensure Migrations directory exists
        if (!Directory.Exists(_migrationsDirectory))
        {
            Directory.CreateDirectory(_migrationsDirectory);
        }
    }

    /// <summary>
    /// Migrates all JSON files from the WebhookData directory to the database
    /// </summary>
    public async Task<MigrationResult> MigrateAllJsonFilesAsync()
    {
        var result = new MigrationResult();

        if (!Directory.Exists(_jsonDirectory))
        {
            _logger.LogWarning("WebhookData directory not found: {Directory}", _jsonDirectory);
            return result;
        }

        var jsonFiles = Directory.GetFiles(_jsonDirectory, "*.json");
        _logger.LogInformation("Found {Count} JSON files to migrate", jsonFiles.Length);

        foreach (var filePath in jsonFiles.OrderBy(f => f))
        {
            try
            {
                await MigrateFileAsync(filePath, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error migrating file: {FilePath}", filePath);
                result.FailedFiles.Add(filePath);
            }
        }

        _logger.LogInformation("Migration completed: {Success} successful, {Failed} failed",
            result.SuccessfulFiles.Count, result.FailedFiles.Count);

        // Save migration report to file
        await SaveMigrationReportAsync(result);

        return result;
    }

    private async Task SaveMigrationReportAsync(MigrationResult result)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var reportFileName = $"migration_report_{timestamp}.json";
        var reportFilePath = Path.Combine(_migrationsDirectory, reportFileName);

        var report = new
        {
            MigrationDate = DateTime.UtcNow,
            Summary = new
            {
                TotalFiles = result.SuccessfulFiles.Count + result.FailedFiles.Count + result.SkippedFiles.Count,
                Successful = result.SuccessfulFiles.Count,
                Failed = result.FailedFiles.Count,
                Skipped = result.SkippedFiles.Count
            },
            SuccessfulFiles = result.SuccessfulFiles.Select(f => new
            {
                FileName = Path.GetFileName(f),
                FullPath = f
            }).ToList(),
            FailedFiles = result.FailedFiles.Select(f => new
            {
                FileName = Path.GetFileName(f),
                FullPath = f
            }).ToList(),
            SkippedFiles = result.SkippedFiles.Select(f => new
            {
                FileName = Path.GetFileName(f),
                FullPath = f
            }).ToList()
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(report, options);
        await File.WriteAllTextAsync(reportFilePath, json);

        _logger.LogInformation("✓ Migration report saved to: {ReportPath}", reportFilePath);

        // Also create a human-readable summary file
        await SaveMigrationSummaryAsync(result, timestamp);
    }

    private async Task SaveMigrationSummaryAsync(MigrationResult result, string timestamp)
    {
        var summaryFileName = $"migration_summary_{timestamp}.txt";
        var summaryFilePath = Path.Combine(_migrationsDirectory, summaryFileName);

        var summary = new System.Text.StringBuilder();
        summary.AppendLine("=".PadRight(80, '='));
        summary.AppendLine("WEBHOOK MIGRATION REPORT");
        summary.AppendLine("=".PadRight(80, '='));
        summary.AppendLine();
        summary.AppendLine($"Migration Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        summary.AppendLine();
        summary.AppendLine("-".PadRight(80, '-'));
        summary.AppendLine("SUMMARY");
        summary.AppendLine("-".PadRight(80, '-'));
        summary.AppendLine($"Total Files Processed: {result.SuccessfulFiles.Count + result.FailedFiles.Count + result.SkippedFiles.Count}");
        summary.AppendLine($"✓ Successfully Migrated: {result.SuccessfulFiles.Count}");
        summary.AppendLine($"✗ Failed: {result.FailedFiles.Count}");
        summary.AppendLine($"⊘ Skipped (duplicates): {result.SkippedFiles.Count}");
        summary.AppendLine();

        if (result.SuccessfulFiles.Count > 0)
        {
            summary.AppendLine("-".PadRight(80, '-'));
            summary.AppendLine("SUCCESSFULLY MIGRATED FILES");
            summary.AppendLine("-".PadRight(80, '-'));
            foreach (var file in result.SuccessfulFiles)
            {
                summary.AppendLine($"  ✓ {Path.GetFileName(file)}");
            }
            summary.AppendLine();
        }

        if (result.SkippedFiles.Count > 0)
        {
            summary.AppendLine("-".PadRight(80, '-'));
            summary.AppendLine("SKIPPED FILES (Already in Database)");
            summary.AppendLine("-".PadRight(80, '-'));
            foreach (var file in result.SkippedFiles)
            {
                summary.AppendLine($"  ⊘ {Path.GetFileName(file)}");
            }
            summary.AppendLine();
        }

        if (result.FailedFiles.Count > 0)
        {
            summary.AppendLine("-".PadRight(80, '-'));
            summary.AppendLine("FAILED FILES");
            summary.AppendLine("-".PadRight(80, '-'));
            foreach (var file in result.FailedFiles)
            {
                summary.AppendLine($"  ✗ {Path.GetFileName(file)}");
            }
            summary.AppendLine();
        }

        summary.AppendLine("=".PadRight(80, '='));

        await File.WriteAllTextAsync(summaryFilePath, summary.ToString());
        _logger.LogInformation("✓ Migration summary saved to: {SummaryPath}", summaryFilePath);
    }

    private async Task MigrateFileAsync(string filePath, MigrationResult result)
    {
        var fileName = Path.GetFileName(filePath);
        var fileContent = await File.ReadAllTextAsync(filePath);

        using var jsonDoc = JsonDocument.Parse(fileContent);
        var root = jsonDoc.RootElement;

        // Determine the type of JSON file based on structure
        if (root.TryGetProperty("EventType", out var eventTypeProp))
        {
            // This is a wrapped webhook event from our storage
            var eventType = eventTypeProp.GetString() ?? "unknown";
            var receivedAt = root.TryGetProperty("ReceivedAt", out var receivedAtProp)
                ? receivedAtProp.GetDateTime()
                : DateTime.UtcNow;

            if (!root.TryGetProperty("Payload", out var payload))
            {
                _logger.LogWarning("No Payload found in file: {FileName}", fileName);
                result.SkippedFiles.Add(filePath);
                return;
            }

            // Check if this event already exists in the database
            using var scope = _services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<WebhookDbContext>();

            var payloadJson = payload.GetRawText();

            // Check for duplicates based on payload content (simple hash check)
            var exists = await dbContext.WebhookEvents
                .AnyAsync(e => e.RawPayload == payloadJson && e.EventType == eventType);

            if (exists)
            {
                _logger.LogInformation("Skipping duplicate: {FileName}", fileName);
                result.SkippedFiles.Add(filePath);
                return;
            }

            // Create webhook event
            var webhookEvent = new WebhookEvent
            {
                EventType = eventType,
                ReceivedAt = receivedAt,
                RawPayload = payloadJson,
                ObjectType = payload.TryGetProperty("object", out var objProp) ? objProp.GetString() : null,
                IsProcessed = true
            };

            dbContext.WebhookEvents.Add(webhookEvent);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation("✓ Migrated webhook event: {FileName} -> ID={Id}", fileName, webhookEvent.Id);

            // Parse and migrate messages if this is a messaging event
            if (eventType == "messaging" || payload.ValueKind == JsonValueKind.Object)
            {
                await MigrateMessagesFromPayloadAsync(payload, webhookEvent.Id, dbContext);
            }

            result.SuccessfulFiles.Add(filePath);
        }
        else
        {
            _logger.LogWarning("Unknown JSON format in file: {FileName}", fileName);
            result.SkippedFiles.Add(filePath);
        }
    }

    private async Task MigrateMessagesFromPayloadAsync(JsonElement payload, int webhookEventId, WebhookDbContext dbContext)
    {
        try
        {
            // Extract sender, recipient, timestamp
            var senderId = payload.TryGetProperty("sender", out var sender) && sender.TryGetProperty("id", out var senderIdProp)
                ? senderIdProp.GetString() ?? "unknown"
                : "unknown";

            var recipientId = payload.TryGetProperty("recipient", out var recipient) && recipient.TryGetProperty("id", out var recipientIdProp)
                ? recipientIdProp.GetString() ?? "unknown"
                : "unknown";

            var timestamp = payload.TryGetProperty("timestamp", out var timestampProp)
                ? DateTimeOffset.FromUnixTimeMilliseconds(timestampProp.GetInt64()).DateTime
                : DateTime.UtcNow;

            // Check for message
            if (payload.TryGetProperty("message", out var messageData))
            {
                var text = messageData.TryGetProperty("text", out var textProp) ? textProp.GetString() : null;
                var mid = messageData.TryGetProperty("mid", out var midProp) ? midProp.GetString() : null;
                var isEcho = messageData.TryGetProperty("is_echo", out var isEchoProp) && isEchoProp.GetBoolean();
                var appId = messageData.TryGetProperty("app_id", out var appIdProp) ? appIdProp.GetInt64().ToString() : null;

                var message = new Message
                {
                    WebhookEventId = webhookEventId,
                    MessageId = mid,
                    SenderId = senderId,
                    RecipientId = recipientId,
                    Text = text,
                    Timestamp = timestamp,
                    MessageType = "message",
                    IsEcho = isEcho,
                    AppId = appId,
                    CreatedAt = DateTime.UtcNow,
                    IsResponded = false
                };

                dbContext.Messages.Add(message);
                await dbContext.SaveChangesAsync();
                _logger.LogInformation("  ✓ Migrated message: {MessageId}", mid);
            }
            // Check for delivery
            else if (payload.TryGetProperty("delivery", out var delivery))
            {
                var watermark = delivery.TryGetProperty("watermark", out var watermarkProp) ? watermarkProp.GetInt64() : (long?)null;

                var message = new Message
                {
                    WebhookEventId = webhookEventId,
                    SenderId = senderId,
                    RecipientId = recipientId,
                    Timestamp = timestamp,
                    MessageType = "delivery",
                    DeliveryWatermark = watermark,
                    CreatedAt = DateTime.UtcNow,
                    IsResponded = false
                };

                dbContext.Messages.Add(message);
                await dbContext.SaveChangesAsync();
                _logger.LogInformation("  ✓ Migrated delivery confirmation");
            }
            // Check for postback
            else if (payload.TryGetProperty("postback", out var postback))
            {
                var payloadStr = postback.TryGetProperty("payload", out var payloadProp) ? payloadProp.GetString() : null;

                var message = new Message
                {
                    WebhookEventId = webhookEventId,
                    SenderId = senderId,
                    RecipientId = recipientId,
                    Timestamp = timestamp,
                    MessageType = "postback",
                    PostbackPayload = payloadStr,
                    CreatedAt = DateTime.UtcNow,
                    IsResponded = false
                };

                dbContext.Messages.Add(message);
                await dbContext.SaveChangesAsync();
                _logger.LogInformation("  ✓ Migrated postback");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error migrating messages from payload");
        }
    }
}

public class MigrationResult
{
    public List<string> SuccessfulFiles { get; set; } = new();
    public List<string> FailedFiles { get; set; } = new();
    public List<string> SkippedFiles { get; set; } = new();
}
