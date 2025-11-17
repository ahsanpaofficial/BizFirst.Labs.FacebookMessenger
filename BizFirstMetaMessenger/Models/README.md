# Models

This directory contains all data transfer objects (DTOs) and configuration models used throughout the BizFirstMetaMessenger application.

---

## Table of Contents

- [Overview](#overview)
- [Models](#models)
- [Usage](#usage)
- [Validation](#validation)

---

## Overview

Models define the structure of data exchanged between:
- **Meta's Messenger Platform** and **Your Application** (webhook events)
- **Controllers** and **Services**
- **Application** and **Configuration Files**

---

## Models

### 1. MetaConfiguration

**File:** `MetaConfiguration.cs`
**Purpose:** Configuration model for Meta Messenger credentials

#### Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| VerifyToken | string? | Yes | Token for webhook verification handshake |
| AppSecret | string? | Yes | App secret for HMAC signature validation |
| PageAccessToken | string? | No | Optional token for sending messages |

#### Configuration

Loaded from `appsettings.json`:

```json
{
  "Meta": {
    "VerifyToken": "bizfirst_webhook_verify_token_2024",
    "AppSecret": "ee8ded0cf2b49c47471c062cb1d3d78c",
    "PageAccessToken": "optional-page-access-token"
  }
}
```

Or from environment variables:
```bash
export META_VERIFY_TOKEN="your_token"
export META_APP_SECRET="your_secret"
```

#### Usage

```csharp
public class WebhookController : ControllerBase
{
    private readonly MetaConfiguration _config;

    public WebhookController(IOptions<MetaConfiguration> config)
    {
        _config = config.Value;
    }

    public IActionResult VerifyWebhook(string token)
    {
        if (token == _config.VerifyToken)
        {
            // Verified!
        }
    }
}
```

**Code Reference:** `MetaConfiguration.cs:6`

---

### 2. WebhookEventRequest

**File:** `WebhookEventRequest.cs`
**Purpose:** Model for incoming webhook events from Meta

#### Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| Object | string | Yes | Object type (usually "page") |
| Entry | JsonElement[]? | No | Array of entry objects |
| RawBody | string? | No | Raw JSON payload (set by controller) |
| SignatureHeader | string? | No | HMAC signature (set by controller) |

#### Structure

```csharp
public class WebhookEventRequest
{
    public string Object { get; set; } = string.Empty;
    public JsonElement[]? Entry { get; set; }
    public string? RawBody { get; set; }
    public string? SignatureHeader { get; set; }
}
```

#### Example JSON

```json
{
  "object": "page",
  "entry": [
    {
      "id": "123456789",
      "time": 1234567890,
      "messaging": [
        {
          "sender": {"id": "USER_ID"},
          "recipient": {"id": "PAGE_ID"},
          "timestamp": 1234567890,
          "message": {
            "mid": "mid.1234567890",
            "text": "Hello!"
          }
        }
      ]
    }
  ]
}
```

#### Entry Structure

Each entry contains:
- **id** - Page ID
- **time** - Timestamp
- **messaging** - Array of messaging events
- **changes** - Array of field changes (for page updates)

#### Messaging Event Types

**Message Event:**
```json
{
  "sender": {"id": "USER_ID"},
  "recipient": {"id": "PAGE_ID"},
  "timestamp": 1234567890,
  "message": {
    "mid": "mid.123",
    "text": "Hello!",
    "attachments": []
  }
}
```

**Postback Event:**
```json
{
  "sender": {"id": "USER_ID"},
  "recipient": {"id": "PAGE_ID"},
  "timestamp": 1234567890,
  "postback": {
    "title": "Button Title",
    "payload": "BUTTON_PAYLOAD"
  }
}
```

**Delivery Event:**
```json
{
  "sender": {"id": "USER_ID"},
  "recipient": {"id": "PAGE_ID"},
  "delivery": {
    "mids": ["mid.123"],
    "watermark": 1234567890
  }
}
```

**Read Event:**
```json
{
  "sender": {"id": "USER_ID"},
  "recipient": {"id": "PAGE_ID"},
  "read": {
    "watermark": 1234567890
  }
}
```

**Echo Event (Message sent by page):**
```json
{
  "sender": {"id": "PAGE_ID"},
  "recipient": {"id": "USER_ID"},
  "timestamp": 1234567890,
  "message": {
    "is_echo": true,
    "app_id": "YOUR_APP_ID",
    "mid": "mid.123",
    "text": "Response from bot"
  }
}
```

**Code Reference:** `WebhookEventRequest.cs:8`

---

### 3. WebhookVerificationRequest

**File:** `WebhookVerificationRequest.cs`
**Purpose:** Model for Meta's webhook verification GET request

#### Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| Mode | string | Yes | Must be "subscribe" |
| VerifyToken | string | Yes | Your verification token |
| Challenge | string | Yes | Random string from Meta |

#### Query Parameters

Meta sends:
```
GET /webhook?hub.mode=subscribe&hub.verify_token=your_token&hub.challenge=random_string
```

#### Controller Binding

```csharp
[HttpGet]
public IActionResult VerifyWebhook(
    [FromQuery(Name = "hub.mode")] string mode,
    [FromQuery(Name = "hub.verify_token")] string verifyToken,
    [FromQuery(Name = "hub.challenge")] string challenge)
{
    if (mode == "subscribe" && verifyToken == _config.VerifyToken)
    {
        return Content(challenge, "text/plain");
    }
    return StatusCode(403);
}
```

**Code Reference:** `WebhookVerificationRequest.cs:6`

---

### 4. FieldUpdateEvent

**File:** `FieldUpdateEvent.cs`
**Purpose:** Model for page field update events

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| Field | string | Field name that changed |
| Value | JsonElement | New value |

#### Example

When a page's "about" field changes:

```json
{
  "object": "page",
  "entry": [
    {
      "id": "123456789",
      "time": 1234567890,
      "changes": [
        {
          "field": "about",
          "value": "New about text"
        }
      ]
    }
  ]
}
```

#### Supported Fields

Meta may send updates for:
- `about` - Page description
- `description` - Page details
- `name` - Page name
- `category` - Page category
- `picture` - Profile picture
- `cover` - Cover photo
- And more...

**Code Reference:** `FieldUpdateEvent.cs:6`

---

## Usage

### Receiving Webhooks

```csharp
[HttpPost]
public async Task<IActionResult> ReceiveWebhook([FromBody] WebhookEventRequest webhookEvent)
{
    // Validate signature
    var signature = Request.Headers["X-Hub-Signature-256"].ToString();
    var isValid = _validator.ValidateSignature(_config.AppSecret, rawBody, signature);

    if (!isValid)
    {
        return StatusCode(403);
    }

    // Process event
    await _webhookService.ProcessWebhookEventAsync(webhookEvent);

    return Ok(new { status = "received" });
}
```

---

### Accessing Entry Data

```csharp
if (webhookEvent.Entry != null)
{
    foreach (var entry in webhookEvent.Entry)
    {
        if (entry.TryGetProperty("messaging", out var messaging))
        {
            foreach (var msg in messaging.EnumerateArray())
            {
                var senderId = msg.GetProperty("sender").GetProperty("id").GetString();
                var text = msg.GetProperty("message").GetProperty("text").GetString();

                Console.WriteLine($"Message from {senderId}: {text}");
            }
        }
    }
}
```

---

### Field Updates

```csharp
if (entry.TryGetProperty("changes", out var changes))
{
    foreach (var change in changes.EnumerateArray())
    {
        var field = change.GetProperty("field").GetString();
        var value = change.GetProperty("value").GetString();

        _logger.LogInformation("Field '{Field}' changed to '{Value}'", field, value);
    }
}
```

---

## Validation

### Model Validation

Models use data annotations for validation:

```csharp
using System.ComponentModel.DataAnnotations;

public class WebhookEventRequest
{
    [Required]
    public string Object { get; set; } = string.Empty;

    public JsonElement[]? Entry { get; set; }
}
```

### Controller Validation

```csharp
[HttpPost]
public IActionResult ReceiveWebhook([FromBody] WebhookEventRequest request)
{
    if (!ModelState.IsValid)
    {
        return BadRequest(ModelState);
    }

    // Process...
}
```

---

## JSON Serialization

### System.Text.Json

The application uses `System.Text.Json` for JSON serialization:

```csharp
using System.Text.Json;

// Deserialize
var webhook = JsonSerializer.Deserialize<WebhookEventRequest>(json);

// Serialize
var json = JsonSerializer.Serialize(webhook);
```

### JsonElement

`JsonElement` is used for dynamic/flexible data:

```csharp
if (entry.TryGetProperty("messaging", out JsonElement messaging))
{
    // Access nested properties
}
```

---

## Best Practices

### 1. Null Safety

Always check for null values:

```csharp
if (webhookEvent.Entry != null)
{
    foreach (var entry in webhookEvent.Entry)
    {
        // Process entry
    }
}
```

### 2. Property Existence

Check if properties exist before accessing:

```csharp
if (message.TryGetProperty("text", out var textElement))
{
    var text = textElement.GetString();
}
```

### 3. Type Safety

Use proper type checks:

```csharp
if (entry.ValueKind == JsonValueKind.Object)
{
    // Safe to access properties
}
```

---

## Examples

### Example 1: Process Message

```csharp
public async Task ProcessMessage(JsonElement message)
{
    var senderId = message.GetProperty("sender").GetProperty("id").GetString();
    var recipientId = message.GetProperty("recipient").GetProperty("id").GetString();

    if (message.TryGetProperty("message", out var msg))
    {
        var mid = msg.GetProperty("mid").GetString();
        var text = msg.TryGetProperty("text", out var t) ? t.GetString() : null;

        _logger.LogInformation(
            "Message {Mid} from {SenderId}: {Text}",
            mid, senderId, text
        );
    }
}
```

---

### Example 2: Handle Postback

```csharp
public async Task ProcessPostback(JsonElement messaging)
{
    if (messaging.TryGetProperty("postback", out var postback))
    {
        var payload = postback.GetProperty("payload").GetString();
        var title = postback.TryGetProperty("title", out var t) ? t.GetString() : null;

        _logger.LogInformation(
            "Postback received: {Title} with payload {Payload}",
            title, payload
        );

        // Handle button click based on payload
        await HandleButtonClick(payload);
    }
}
```

---

### Example 3: Track Delivery

```csharp
public void ProcessDelivery(JsonElement messaging)
{
    if (messaging.TryGetProperty("delivery", out var delivery))
    {
        var watermark = delivery.GetProperty("watermark").GetInt64();

        if (delivery.TryGetProperty("mids", out var mids))
        {
            foreach (var mid in mids.EnumerateArray())
            {
                _logger.LogInformation(
                    "Message {Mid} delivered at {Watermark}",
                    mid.GetString(), watermark
                );
            }
        }
    }
}
```

---

## Related Documentation

- [Main README](../README.md)
- [Controllers Documentation](../Controllers/README.md)
- [Services Documentation](../Services/README.md)
- [Data Layer Documentation](../Data/README.md)

---

**[← Back to Main README](../README.md)**
