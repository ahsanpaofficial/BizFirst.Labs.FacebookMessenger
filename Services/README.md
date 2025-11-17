# Services

This directory contains the business logic layer of the BizFirstMetaMessenger application. Services handle webhook processing, signature validation, data storage, and database operations.

---

## Table of Contents

- [Overview](#overview)
- [Service Architecture](#service-architecture)
- [Services](#services)
- [Dependency Injection](#dependency-injection)
- [Usage Examples](#usage-examples)

---

## Overview

The services layer follows **SOLID principles** and uses **Dependency Injection** to promote testability and maintainability. Each service has a corresponding interface for loose coupling.

---

## Service Architecture

```
┌────────────────────────────────────────────────────────────┐
│                      Controllers                            │
└─────────────────────┬──────────────────────────────────────┘
                      │
         ┌────────────┴────────────┐
         ▼                         ▼
┌──────────────────┐      ┌──────────────────┐
│ ISignatureValidator│     │  IWebhookService │
└────────┬───────────┘      └────────┬─────────┘
         │                           │
         │                   ┌───────┴────────┐
         │                   ▼                ▼
         │         ┌──────────────────┐ ┌──────────────────┐
         │         │IWebhookStorage   │ │IWebhookDatabase  │
         │         │Service           │ │Service           │
         │         └──────────────────┘ └──────────────────┘
         │                   │                │
         ▼                   ▼                ▼
    Implementations    JSON Files         SQLite DB
```

---

## Services

### 1. SignatureValidator

**Interface:** `ISignatureValidator`
**Implementation:** `SignatureValidator.cs`
**Purpose:** Validate HMAC signatures from Meta webhooks

#### Methods

```csharp
bool ValidateSignature(string appSecret, string payload, string signatureHeader)
```

**Parameters:**
- `appSecret` - Your Meta app secret (from configuration)
- `payload` - Raw request body as string
- `signatureHeader` - Value from `X-Hub-Signature-256` or `X-Hub-Signature` header

**Returns:**
- `true` if signature is valid
- `false` if signature is invalid, null, or malformed

#### Algorithm

1. Parse signature format (`sha256=...` or `sha1=...`)
2. Compute HMAC using the same algorithm
3. Use constant-time comparison to prevent timing attacks

#### Example

```csharp
var isValid = _signatureValidator.ValidateSignature(
    appSecret: "your_app_secret",
    payload: "{\"object\":\"page\"}",
    signatureHeader: "sha256=5d41402abc4b2a76b9719d911017c592"
);

if (!isValid)
{
    return StatusCode(403, new { error = "Invalid signature" });
}
```

#### Security Features

- **Constant-time comparison** - Prevents timing attacks
- **Supports SHA256 and SHA1** - SHA256 is preferred
- **Null/empty validation** - Returns false for missing signatures

**Code Reference:** `SignatureValidator.cs:21`

---

### 2. WebhookService

**Interface:** `IWebhookService`
**Implementation:** `WebhookService.cs`
**Purpose:** Process webhook events and extract message data

#### Methods

```csharp
Task ProcessWebhookEventAsync(WebhookEventRequest webhookEvent)
```

**Parameters:**
- `webhookEvent` - Parsed webhook event from request body

**Processing Steps:**
1. Save raw webhook to database (WebhookEvent table)
2. Iterate through entries and messaging events
3. Extract message details (sender, recipient, text, timestamp)
4. Save to JSON file (backup)
5. Save to database (Message table)
6. Log processing details

#### Handles Event Types

- **Messages** - User-sent text messages
- **Echoes** - Messages sent by your page (app_id present)
- **Postbacks** - Button clicks and quick reply selections
- **Deliveries** - Message delivery confirmations
- **Reads** - Message read receipts

#### Example

```csharp
await _webhookService.ProcessWebhookEventAsync(new WebhookEventRequest
{
    Object = "page",
    Entry = entries,
    RawBody = rawJson,
    SignatureHeader = "sha256=..."
});
```

**Code Reference:** `WebhookService.cs:29`

---

### 3. WebhookStorageService

**Interface:** `IWebhookStorageService`
**Implementation:** `WebhookStorageService.cs`
**Purpose:** Save webhook data to JSON files for backup/audit

#### Methods

##### SaveWebhookDataAsync

```csharp
Task<string> SaveWebhookDataAsync(string eventType, string jsonPayload)
```

**Parameters:**
- `eventType` - Type of event (e.g., "messaging", "webhook")
- `jsonPayload` - Raw JSON string to save

**Returns:** Full path to saved file

**File Naming Convention:**
```
{eventType}_{yyyyMMdd}_{HHmmss}_{fff}.json

Example: messaging_20251114_194201_034.json
```

##### GetWebhookFilesAsync

```csharp
Task<IEnumerable<string>> GetWebhookFilesAsync()
```

**Returns:** List of all JSON file paths in WebhookData directory

#### Configuration

Set storage directory in `appsettings.json`:
```json
{
  "WebhookStorage": {
    "Directory": "WebhookData"
  }
}
```

**Code Reference:** `WebhookStorageService.cs:20`

---

### 4. WebhookDatabaseService

**Interface:** `IWebhookDatabaseService`
**Implementation:** `WebhookDatabaseService.cs`
**Purpose:** Manage database operations for webhook events and messages

#### Methods

##### SaveWebhookEventAsync

```csharp
Task<int> SaveWebhookEventAsync(string eventType, string rawPayload, string? objectType = null)
```

**Parameters:**
- `eventType` - Event type (e.g., "webhook", "messaging")
- `rawPayload` - Raw JSON payload
- `objectType` - Object type from webhook (e.g., "page")

**Returns:** Database ID of the saved event

---

##### SaveMessageAsync

```csharp
Task SaveMessageAsync(int webhookEventId, Message message)
```

**Parameters:**
- `webhookEventId` - Foreign key to WebhookEvent
- `message` - Message entity to save

---

##### GetWebhookEventsAsync

```csharp
Task<List<WebhookEvent>> GetWebhookEventsAsync(DateTime? since = null, bool? isProcessed = null)
```

**Parameters:**
- `since` - Optional filter for events after this date
- `isProcessed` - Optional filter for processed status

**Returns:** List of webhook events with related messages

---

##### GetMessagesAsync

```csharp
Task<List<Message>> GetMessagesAsync(string? senderId = null, DateTime? since = null)
```

**Parameters:**
- `senderId` - Optional filter by sender ID
- `since` - Optional filter for messages after this date

**Returns:** List of messages

---

##### GetUnrespondedMessagesAsync

```csharp
Task<List<Message>> GetUnrespondedMessagesAsync()
```

**Returns:** Messages that haven't been responded to yet

---

##### GetDatabaseStatsAsync

```csharp
Task<object> GetDatabaseStatsAsync()
```

**Returns:** Statistics object with counts and event types

**Example Response:**
```json
{
  "totalWebhookEvents": 15,
  "totalMessages": 7,
  "unrespondedMessages": 1,
  "eventsByType": {
    "messaging": 8,
    "webhook": 7
  }
}
```

**Code Reference:** `WebhookDatabaseService.cs:17`

---

### 5. WebhookMigrationService

**File:** `WebhookMigrationService.cs`
**Purpose:** Migrate JSON files from WebhookData/ to database

#### Methods

##### MigrateJsonFilesToDatabaseAsync

```csharp
Task<MigrationResult> MigrateJsonFilesToDatabaseAsync()
```

**Returns:** Migration result with success/failed/skipped counts

**Processing:**
1. Scan WebhookData/ directory for JSON files
2. Read and parse each file
3. Check for duplicates (using file hash)
4. Insert into database if not exists
5. Generate migration report

**Duplicate Detection:**
- Uses SHA256 hash of file path to detect duplicates
- Skips files already migrated
- Logs skipped files in migration report

**Migration Reports:**
- **JSON Report:** `Migrations/migration_report_{timestamp}.json`
- **Text Summary:** `Migrations/migration_summary_{timestamp}.txt`

#### Migration Result

```csharp
public class MigrationResult
{
    public DateTime MigrationDate { get; set; }
    public MigrationSummary Summary { get; set; }
    public List<FileInfo> SuccessfulFiles { get; set; }
    public List<FileInfo> FailedFiles { get; set; }
    public List<FileInfo> SkippedFiles { get; set; }
}
```

**Code Reference:** `WebhookMigrationService.cs:22`

---

## Dependency Injection

Services are registered in `Program.cs`:

```csharp
// Register services
builder.Services.AddScoped<ISignatureValidator, SignatureValidator>();
builder.Services.AddScoped<IWebhookService, WebhookService>();
builder.Services.AddSingleton<IWebhookStorageService, WebhookStorageService>();
builder.Services.AddScoped<IWebhookDatabaseService, WebhookDatabaseService>();
```

### Service Lifetimes

- **Scoped** - One instance per request
  - `SignatureValidator`
  - `WebhookService`
  - `WebhookDatabaseService`

- **Singleton** - One instance for application lifetime
  - `WebhookStorageService`

---

## Usage Examples

### Example 1: Validate Signature

```csharp
public class WebhookController : ControllerBase
{
    private readonly ISignatureValidator _validator;
    private readonly MetaConfiguration _config;

    public WebhookController(ISignatureValidator validator, IOptions<MetaConfiguration> config)
    {
        _validator = validator;
        _config = config.Value;
    }

    [HttpPost]
    public async Task<IActionResult> ReceiveWebhook()
    {
        var signature = Request.Headers["X-Hub-Signature-256"].ToString();
        var rawBody = await ReadBodyAsync();

        if (!_validator.ValidateSignature(_config.AppSecret, rawBody, signature))
        {
            return StatusCode(403, new { error = "Invalid signature" });
        }

        // Process webhook...
        return Ok(new { status = "received" });
    }
}
```

---

### Example 2: Save to Database

```csharp
public class WebhookService : IWebhookService
{
    private readonly IWebhookDatabaseService _database;

    public async Task ProcessWebhookEventAsync(WebhookEventRequest request)
    {
        // Save raw webhook
        var eventId = await _database.SaveWebhookEventAsync(
            eventType: "webhook",
            rawPayload: request.RawBody,
            objectType: request.Object
        );

        // Save message
        var message = new Message
        {
            MessageId = "mid.123",
            SenderId = "USER_ID",
            RecipientId = "PAGE_ID",
            Text = "Hello!",
            Timestamp = DateTime.UtcNow,
            MessageType = "message"
        };

        await _database.SaveMessageAsync(eventId, message);
    }
}
```

---

### Example 3: Query Database

```csharp
// Get recent messages
var messages = await _database.GetMessagesAsync(
    since: DateTime.UtcNow.AddDays(-7)
);

// Get unresponded messages
var pending = await _database.GetUnrespondedMessagesAsync();

// Get database statistics
var stats = await _database.GetDatabaseStatsAsync();
```

---

### Example 4: Migration

```csharp
var migration = new WebhookMigrationService(_database, _storage, _logger);
var result = await migration.MigrateJsonFilesToDatabaseAsync();

Console.WriteLine($"Migrated: {result.Summary.Successful}");
Console.WriteLine($"Failed: {result.Summary.Failed}");
Console.WriteLine($"Skipped: {result.Summary.Skipped}");
```

---

## Testing Services

### Unit Tests

See `UnitTests/SignatureValidatorTests.cs` for comprehensive service tests.

**Run tests:**
```bash
cd UnitTests
dotnet test --filter FullyQualifiedName~SignatureValidatorTests
```

### Integration Testing

```csharp
[Fact]
public async Task SaveWebhookEvent_ShouldStoreInDatabase()
{
    // Arrange
    var dbService = new WebhookDatabaseService(_dbContext, _logger);

    // Act
    var eventId = await dbService.SaveWebhookEventAsync(
        "test",
        "{\"test\":\"data\"}",
        "page"
    );

    // Assert
    Assert.True(eventId > 0);
    var saved = await _dbContext.WebhookEvents.FindAsync(eventId);
    Assert.NotNull(saved);
    Assert.Equal("test", saved.EventType);
}
```

---

## Error Handling

All services implement comprehensive error handling:

```csharp
try
{
    await _database.SaveWebhookEventAsync(eventType, payload);
}
catch (DbUpdateException ex)
{
    _logger.LogError(ex, "Database error while saving webhook event");
    throw;
}
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error processing webhook");
    throw;
}
```

---

## Logging

Services use structured logging:

```csharp
_logger.LogInformation("✓ Saved webhook event ID {EventId}", eventId);
_logger.LogWarning("Signature validation failed for payload size {Size}", payload.Length);
_logger.LogError("Failed to save message: {Error}", ex.Message);
```

View logs:
```bash
dotnet run --urls "http://localhost:5234"
```

---

## Related Documentation

- [Main README](../README.md)
- [Controllers Documentation](../Controllers/README.md)
- [Models Documentation](../Models/README.md)
- [Data Layer Documentation](../Data/README.md)

---

**[← Back to Main README](../README.md)**
