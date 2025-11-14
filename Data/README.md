# Data Layer

This directory contains Entity Framework Core entities, database context, and data access configuration for the BizFirstMetaMessenger application.

---

## Table of Contents

- [Overview](#overview)
- [Database](#database)
- [Entities](#entities)
- [Database Context](#database-context)
- [Queries](#queries)
- [Migrations](#migrations)

---

## Overview

The data layer uses **Entity Framework Core** with **SQLite** for persistent storage of webhook events and messages. This provides:

- **Structured storage** - Relational data with foreign keys
- **Query capabilities** - LINQ queries for filtering and aggregation
- **Data integrity** - Constraints and indexes
- **Easy migration** - From JSON files to database

---

## Database

### Database Provider

**SQLite** - Lightweight, serverless, file-based database

### Database Location

```
/path/to/project/bin/Debug/net8.0/webhooks.db
```

### Configuration

Set in `Program.cs`:

```csharp
builder.Services.AddDbContext<WebhookDbContext>(options =>
{
    var dbPath = Path.Combine(AppContext.BaseDirectory, "webhooks.db");
    options.UseSqlite($"Data Source={dbPath}");
});
```

### Database Schema

```sql
-- WebhookEvents table
CREATE TABLE WebhookEvents (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    EventType TEXT NOT NULL,
    ReceivedAt DATETIME NOT NULL,
    RawPayload TEXT NOT NULL,
    ObjectType TEXT,
    IsProcessed INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IX_WebhookEvents_EventType ON WebhookEvents(EventType);
CREATE INDEX IX_WebhookEvents_ReceivedAt ON WebhookEvents(ReceivedAt);

-- Messages table
CREATE TABLE Messages (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    WebhookEventId INTEGER NOT NULL,
    MessageId TEXT,
    SenderId TEXT NOT NULL,
    RecipientId TEXT NOT NULL,
    Text TEXT,
    Timestamp DATETIME NOT NULL,
    MessageType TEXT NOT NULL,
    IsEcho INTEGER NOT NULL DEFAULT 0,
    AppId TEXT,
    PostbackPayload TEXT,
    DeliveryWatermark INTEGER,
    CreatedAt DATETIME NOT NULL,
    IsResponded INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (WebhookEventId) REFERENCES WebhookEvents(Id) ON DELETE CASCADE
);

CREATE INDEX IX_Messages_WebhookEventId ON Messages(WebhookEventId);
CREATE INDEX IX_Messages_SenderId ON Messages(SenderId);
CREATE INDEX IX_Messages_Timestamp ON Messages(Timestamp);
CREATE INDEX IX_Messages_IsResponded ON Messages(IsResponded);
```

---

## Entities

### 1. WebhookEvent

**File:** `WebhookEvent.cs`
**Purpose:** Stores raw webhook events from Meta

#### Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| Id | int | PK | Auto-increment primary key |
| EventType | string | Yes | Event type (e.g., "webhook", "messaging") |
| ReceivedAt | DateTime | Yes | When event was received (UTC) |
| RawPayload | string | Yes | Complete JSON payload |
| ObjectType | string? | No | Object type from webhook (e.g., "page") |
| IsProcessed | bool | Yes | Whether event has been processed |
| Messages | ICollection<Message> | No | Related messages (navigation property) |

#### Entity Definition

```csharp
public class WebhookEvent
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string EventType { get; set; } = string.Empty;

    [Required]
    public DateTime ReceivedAt { get; set; }

    [Required]
    public string RawPayload { get; set; } = string.Empty;

    public string? ObjectType { get; set; }

    public bool IsProcessed { get; set; } = false;

    // Navigation property
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
```

#### Indexes

- `EventType` - For filtering by event type
- `ReceivedAt` - For time-based queries

**Code Reference:** `WebhookEvent.cs:10`

---

### 2. Message

**File:** `Message.cs`
**Purpose:** Stores parsed message data from webhook events

#### Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| Id | int | PK | Auto-increment primary key |
| WebhookEventId | int | FK | Foreign key to WebhookEvent |
| MessageId | string? | No | Meta's message ID (mid) |
| SenderId | string | Yes | User or page ID who sent message |
| RecipientId | string | Yes | User or page ID who received message |
| Text | string? | No | Message text content |
| Timestamp | DateTime | Yes | Message timestamp |
| MessageType | string | Yes | Type: "message", "postback", "delivery", "read" |
| IsEcho | bool | Yes | True if message sent by page |
| AppId | string? | No | App ID (for echo messages) |
| PostbackPayload | string? | No | Postback payload (for button clicks) |
| DeliveryWatermark | long? | No | Delivery watermark timestamp |
| CreatedAt | DateTime | Yes | When record was created (UTC) |
| IsResponded | bool | Yes | Whether message has been responded to |
| WebhookEvent | WebhookEvent? | No | Navigation property to parent event |

#### Entity Definition

```csharp
public class Message
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int WebhookEventId { get; set; }

    public string? MessageId { get; set; }

    [Required]
    public string SenderId { get; set; } = string.Empty;

    [Required]
    public string RecipientId { get; set; } = string.Empty;

    public string? Text { get; set; }

    [Required]
    public DateTime Timestamp { get; set; }

    [Required]
    public string MessageType { get; set; } = string.Empty;

    public bool IsEcho { get; set; } = false;

    public string? AppId { get; set; }

    public string? PostbackPayload { get; set; }

    public long? DeliveryWatermark { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsResponded { get; set; } = false;

    // Navigation property
    public WebhookEvent? WebhookEvent { get; set; }
}
```

#### Indexes

- `WebhookEventId` - For joining with parent events
- `SenderId` - For filtering by sender
- `Timestamp` - For time-based queries
- `IsResponded` - For finding pending messages

**Code Reference:** `Message.cs:10`

---

## Database Context

### WebhookDbContext

**File:** `WebhookDbContext.cs`
**Purpose:** EF Core database context for webhook data

#### DbSets

```csharp
public DbSet<WebhookEvent> WebhookEvents { get; set; }
public DbSet<Message> Messages { get; set; }
```

#### Configuration

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // WebhookEvent configuration
    modelBuilder.Entity<WebhookEvent>(entity =>
    {
        entity.HasKey(e => e.Id);

        entity.HasIndex(e => e.EventType);
        entity.HasIndex(e => e.ReceivedAt);

        // One-to-many relationship
        entity.HasMany(e => e.Messages)
            .WithOne(m => m.WebhookEvent)
            .HasForeignKey(m => m.WebhookEventId)
            .OnDelete(DeleteBehavior.Cascade);
    });

    // Message configuration
    modelBuilder.Entity<Message>(entity =>
    {
        entity.HasKey(e => e.Id);

        entity.HasIndex(e => e.WebhookEventId);
        entity.HasIndex(e => e.SenderId);
        entity.HasIndex(e => e.Timestamp);
        entity.HasIndex(e => e.IsResponded);
    });
}
```

#### Relationships

**One-to-Many:**
- One `WebhookEvent` has many `Messages`
- Cascade delete: Deleting a webhook event deletes all related messages

**Code Reference:** `WebhookDbContext.cs:12`

---

## Queries

### Basic Queries

#### Get All Webhook Events

```csharp
var events = await _context.WebhookEvents
    .Include(e => e.Messages)
    .ToListAsync();
```

#### Get Recent Events

```csharp
var recentEvents = await _context.WebhookEvents
    .Where(e => e.ReceivedAt >= DateTime.UtcNow.AddDays(-7))
    .OrderByDescending(e => e.ReceivedAt)
    .ToListAsync();
```

#### Get Unprocessed Events

```csharp
var pending = await _context.WebhookEvents
    .Where(e => !e.IsProcessed)
    .ToListAsync();
```

---

### Message Queries

#### Get Messages from Specific User

```csharp
var userMessages = await _context.Messages
    .Where(m => m.SenderId == "USER_ID")
    .OrderBy(m => m.Timestamp)
    .ToListAsync();
```

#### Get Unresponded Messages

```csharp
var pending = await _context.Messages
    .Where(m => !m.IsResponded && !m.IsEcho)
    .OrderBy(m => m.Timestamp)
    .ToListAsync();
```

#### Get Messages with Webhook Event

```csharp
var messages = await _context.Messages
    .Include(m => m.WebhookEvent)
    .Where(m => m.Timestamp >= DateTime.UtcNow.AddHours(-24))
    .ToListAsync();
```

---

### Aggregation Queries

#### Count by Event Type

```csharp
var counts = await _context.WebhookEvents
    .GroupBy(e => e.EventType)
    .Select(g => new
    {
        EventType = g.Key,
        Count = g.Count()
    })
    .ToListAsync();
```

#### Messages per Day

```csharp
var messagesPerDay = await _context.Messages
    .GroupBy(m => m.Timestamp.Date)
    .Select(g => new
    {
        Date = g.Key,
        Count = g.Count()
    })
    .OrderBy(x => x.Date)
    .ToListAsync();
```

---

## Migrations

### Database Creation

The database is automatically created on first run:

```csharp
// In Program.cs
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<WebhookDbContext>();
    dbContext.Database.EnsureCreated();
}
```

### Schema Updates

For schema changes, use EF Core migrations:

```bash
# Install EF Core tools
dotnet tool install --global dotnet-ef

# Create migration
dotnet ef migrations add InitialCreate

# Apply migration
dotnet ef database update
```

### Reset Database

Delete the database file and restart:

```bash
rm bin/Debug/net8.0/webhooks.db
dotnet run
```

---

## Database Management

### View Data with SQLite CLI

```bash
# Open database
sqlite3 bin/Debug/net8.0/webhooks.db

# List tables
.tables

# View schema
.schema WebhookEvents
.schema Messages

# Query data
SELECT * FROM WebhookEvents LIMIT 10;
SELECT * FROM Messages WHERE IsResponded = 0;

# Exit
.exit
```

### View Data with DB Browser

Download **DB Browser for SQLite** (free tool):
```
https://sqlitebrowser.org/
```

Open `webhooks.db` to view and query data visually.

---

## Performance Optimization

### Indexes

All frequently queried columns have indexes:
- `WebhookEvents.EventType`
- `WebhookEvents.ReceivedAt`
- `Messages.SenderId`
- `Messages.Timestamp`
- `Messages.IsResponded`

### Eager Loading

Use `Include()` to avoid N+1 queries:

```csharp
// Good: Single query with join
var events = await _context.WebhookEvents
    .Include(e => e.Messages)
    .ToListAsync();

// Bad: N+1 queries
var events = await _context.WebhookEvents.ToListAsync();
foreach (var e in events)
{
    var messages = e.Messages.ToList(); // Separate query!
}
```

### Async Operations

Always use async methods:

```csharp
// Good
await _context.SaveChangesAsync();
await _context.WebhookEvents.ToListAsync();

// Avoid
_context.SaveChanges(); // Blocks thread
_context.WebhookEvents.ToList(); // Blocks thread
```

---

## Examples

### Example 1: Save Webhook Event

```csharp
var webhookEvent = new WebhookEvent
{
    EventType = "messaging",
    ReceivedAt = DateTime.UtcNow,
    RawPayload = jsonString,
    ObjectType = "page"
};

_context.WebhookEvents.Add(webhookEvent);
await _context.SaveChangesAsync();

Console.WriteLine($"Saved event ID: {webhookEvent.Id}");
```

---

### Example 2: Save Message with Parent Event

```csharp
var message = new Message
{
    WebhookEventId = eventId,
    MessageId = "mid.123",
    SenderId = "USER_ID",
    RecipientId = "PAGE_ID",
    Text = "Hello!",
    Timestamp = DateTime.UtcNow,
    MessageType = "message"
};

_context.Messages.Add(message);
await _context.SaveChangesAsync();
```

---

### Example 3: Update Message Status

```csharp
var message = await _context.Messages.FindAsync(messageId);
if (message != null)
{
    message.IsResponded = true;
    await _context.SaveChangesAsync();
}
```

---

### Example 4: Delete Old Events

```csharp
var cutoffDate = DateTime.UtcNow.AddDays(-30);

var oldEvents = await _context.WebhookEvents
    .Where(e => e.ReceivedAt < cutoffDate && e.IsProcessed)
    .ToListAsync();

_context.WebhookEvents.RemoveRange(oldEvents);
await _context.SaveChangesAsync();

Console.WriteLine($"Deleted {oldEvents.Count} old events");
```

---

## Error Handling

### DbUpdateException

```csharp
try
{
    await _context.SaveChangesAsync();
}
catch (DbUpdateException ex)
{
    _logger.LogError(ex, "Database update failed");
    throw;
}
```

### Concurrency

```csharp
try
{
    await _context.SaveChangesAsync();
}
catch (DbUpdateConcurrencyException ex)
{
    // Handle concurrency conflict
    _logger.LogWarning("Concurrency conflict detected");
}
```

---

## Best Practices

1. **Always use async methods** - Don't block threads
2. **Dispose contexts properly** - Use using statements or DI scoping
3. **Use eager loading** - Avoid N+1 query problems
4. **Add indexes** - For frequently queried columns
5. **Validate before saving** - Check required fields
6. **Handle exceptions** - Log database errors
7. **Use transactions** - For multi-step operations
8. **Keep contexts short-lived** - Don't hold connections open

---

## Related Documentation

- [Main README](../README.md)
- [Services Documentation](../Services/README.md)
- [Migrations Guide](../Migrations/README.md)

---

**[← Back to Main README](../README.md)**
