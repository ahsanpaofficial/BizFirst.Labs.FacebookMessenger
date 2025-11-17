using BizFirstMetaMessenger.Data;
using Microsoft.EntityFrameworkCore;

namespace BizFirstMetaMessenger.Services;

/// <summary>
/// Service for saving and retrieving webhook events from the database
/// </summary>
public class WebhookDatabaseService : IWebhookDatabaseService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<WebhookDatabaseService> _logger;

    public WebhookDatabaseService(IServiceProvider services, ILogger<WebhookDatabaseService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task<int> SaveWebhookEventAsync(string eventType, string rawPayload, string? objectType = null)
    {
        using var scope = _services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WebhookDbContext>();

        var webhookEvent = new WebhookEvent
        {
            EventType = eventType,
            ReceivedAt = DateTime.UtcNow,
            RawPayload = rawPayload,
            ObjectType = objectType,
            IsProcessed = false
        };

        dbContext.WebhookEvents.Add(webhookEvent);
        await dbContext.SaveChangesAsync();

        _logger.LogInformation("✓ Webhook event saved to database: ID={Id}, Type={EventType}",
            webhookEvent.Id, eventType);

        return webhookEvent.Id;
    }

    public async Task<int> SaveMessageAsync(int webhookEventId, Message message)
    {
        using var scope = _services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WebhookDbContext>();

        message.WebhookEventId = webhookEventId;
        message.CreatedAt = DateTime.UtcNow;

        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync();

        _logger.LogInformation("✓ Message saved to database: ID={Id}, Sender={SenderId}, Type={MessageType}",
            message.Id, message.SenderId, message.MessageType);

        return message.Id;
    }

    public async Task<List<WebhookEvent>> GetWebhookEventsAsync(DateTime? startDate = null, DateTime? endDate = null, string? eventType = null)
    {
        using var scope = _services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WebhookDbContext>();

        var query = dbContext.WebhookEvents.AsQueryable();

        if (startDate.HasValue)
        {
            query = query.Where(e => e.ReceivedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(e => e.ReceivedAt <= endDate.Value);
        }

        if (!string.IsNullOrEmpty(eventType))
        {
            query = query.Where(e => e.EventType == eventType);
        }

        return await query
            .Include(e => e.Messages)
            .OrderByDescending(e => e.ReceivedAt)
            .ToListAsync();
    }

    public async Task<List<Message>> GetMessagesAsync(string? senderId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        using var scope = _services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WebhookDbContext>();

        var query = dbContext.Messages.AsQueryable();

        if (!string.IsNullOrEmpty(senderId))
        {
            query = query.Where(m => m.SenderId == senderId);
        }

        if (startDate.HasValue)
        {
            query = query.Where(m => m.Timestamp >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(m => m.Timestamp <= endDate.Value);
        }

        return await query
            .Include(m => m.WebhookEvent)
            .OrderByDescending(m => m.Timestamp)
            .ToListAsync();
    }

    public async Task<List<Message>> GetUnrespondedMessagesAsync()
    {
        using var scope = _services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WebhookDbContext>();

        return await dbContext.Messages
            .Where(m => !m.IsResponded && !m.IsEcho && m.MessageType == "message")
            .Include(m => m.WebhookEvent)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
    }
}
