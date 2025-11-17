using BizFirstMetaMessenger.Services;
using Microsoft.AspNetCore.Mvc;

namespace BizFirstMetaMessenger.Controllers;

/// <summary>
/// Controller for database migration operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MigrationController : ControllerBase
{
    private readonly IServiceProvider _services;
    private readonly ILogger<MigrationController> _logger;

    public MigrationController(IServiceProvider services, ILogger<MigrationController> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <summary>
    /// Migrates all JSON files from WebhookData directory to the database
    /// </summary>
    /// <returns>Migration result with statistics</returns>
    [HttpPost("migrate-json-files")]
    public async Task<IActionResult> MigrateJsonFiles()
    {
        _logger.LogInformation("Starting JSON to database migration...");

        var migrationService = new WebhookMigrationService(
            _services,
            _services.GetRequiredService<ILogger<WebhookMigrationService>>(),
            _services.GetRequiredService<IConfiguration>()
        );

        var result = await migrationService.MigrateAllJsonFilesAsync();

        var response = new
        {
            Success = true,
            Message = "Migration completed",
            Statistics = new
            {
                Successful = result.SuccessfulFiles.Count,
                Failed = result.FailedFiles.Count,
                Skipped = result.SkippedFiles.Count,
                Total = result.SuccessfulFiles.Count + result.FailedFiles.Count + result.SkippedFiles.Count
            },
            Details = new
            {
                SuccessfulFiles = result.SuccessfulFiles.Select(Path.GetFileName).ToList(),
                FailedFiles = result.FailedFiles.Select(Path.GetFileName).ToList(),
                SkippedFiles = result.SkippedFiles.Select(Path.GetFileName).ToList()
            }
        };

        return Ok(response);
    }

    /// <summary>
    /// Gets database statistics
    /// </summary>
    [HttpGet("database-stats")]
    public async Task<IActionResult> GetDatabaseStats()
    {
        using var scope = _services.CreateScope();
        var databaseService = scope.ServiceProvider.GetRequiredService<IWebhookDatabaseService>();

        var allEvents = await databaseService.GetWebhookEventsAsync();
        var allMessages = await databaseService.GetMessagesAsync();
        var unrespondedMessages = await databaseService.GetUnrespondedMessagesAsync();

        var stats = new
        {
            TotalWebhookEvents = allEvents.Count,
            TotalMessages = allMessages.Count,
            UnrespondedMessages = unrespondedMessages.Count,
            MessageTypes = allMessages.GroupBy(m => m.MessageType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToList(),
            RecentEvents = allEvents.Take(5).Select(e => new
            {
                e.Id,
                e.EventType,
                e.ReceivedAt,
                e.ObjectType,
                MessageCount = e.Messages.Count
            }).ToList()
        };

        return Ok(stats);
    }
}
