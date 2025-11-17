# Controllers

This directory contains all API controllers for the BizFirstMetaMessenger webhook application. Controllers handle incoming HTTP requests, validate inputs, and coordinate with services to process webhook events.

---

## Table of Contents

- [Overview](#overview)
- [Controllers](#controllers)
- [Request Flow](#request-flow)
- [Error Handling](#error-handling)
- [Examples](#examples)

---

## Overview

The application uses **ASP.NET Core MVC** pattern with three main controllers:

1. **WebhookController** - Main webhook endpoints for Meta Messenger
2. **MigrationController** - Database migration management
3. **HomeController** - Default welcome/health check endpoint

---

## Controllers

### 1. WebhookController.cs

**Location:** `Controllers/WebhookController.cs`
**Route:** `/webhook`
**Purpose:** Receive and process Meta Messenger webhook events

#### Endpoints

##### GET /webhook - Verification Endpoint

**Purpose:** Meta's webhook verification handshake

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| hub.mode | string | Yes | Must be "subscribe" |
| hub.verify_token | string | Yes | Must match configured token |
| hub.challenge | string | Yes | Random string from Meta |

**Response:**
- **200 OK** - Returns the challenge string (verification successful)
- **403 Forbidden** - Invalid token or mode

**Example Request:**
```http
GET /webhook?hub.mode=subscribe&hub.verify_token=your_token&hub.challenge=12345
```

**Example Response:**
```
12345
```

**Code Reference:** `WebhookController.cs:35`

---

##### POST /webhook - Production Webhook Receiver

**Purpose:** Receive and process webhook events from Meta

**Headers:**
| Header | Required | Description |
|--------|----------|-------------|
| X-Hub-Signature-256 | Yes | HMAC SHA256 signature |
| X-Hub-Signature | No | Legacy SHA1 signature (fallback) |
| Content-Type | Yes | application/json |

**Request Body:**
```json
{
  "object": "page",
  "entry": [
    {
      "id": "PAGE_ID",
      "time": 1234567890,
      "messaging": [
        {
          "sender": {"id": "USER_ID"},
          "recipient": {"id": "PAGE_ID"},
          "timestamp": 1234567890,
          "message": {
            "mid": "MESSAGE_ID",
            "text": "Hello!"
          }
        }
      ]
    }
  ]
}
```

**Response:**
- **200 OK** - Event processed successfully
  ```json
  {"status": "received"}
  ```
- **403 Forbidden** - Invalid or missing signature
  ```json
  {"error": "Invalid signature"}
  ```
- **400 Bad Request** - Missing signature header
  ```json
  {"error": "Missing signature header"}
  ```

**Processing Flow:**
1. Read raw request body
2. Extract signature from headers (X-Hub-Signature-256 or X-Hub-Signature)
3. Validate HMAC signature using `SignatureValidator`
4. Parse webhook event using `WebhookService`
5. Save to JSON file (backup)
6. Save to database
7. Return success response

**Code Reference:** `WebhookController.cs:66`

---

##### POST /webhook/test - Test Endpoint (Development Only)

**Purpose:** Test webhook processing without signature validation

**Headers:**
| Header | Required | Description |
|--------|----------|-------------|
| Content-Type | Yes | application/json |

**Request Body:** Same as production endpoint

**Response:**
```json
{
  "status": "received",
  "message": "Test webhook processed successfully",
  "eventObject": "page",
  "entryCount": 1
}
```

⚠️ **Warning:** This endpoint bypasses signature validation. **DO NOT expose in production!**

**Code Reference:** `WebhookController.cs:122`

---

### 2. MigrationController.cs

**Location:** `Controllers/MigrationController.cs`
**Route:** `/api/Migration`
**Purpose:** Manage migration of JSON files to database

#### Endpoints

##### POST /api/Migration/migrate-json-files

**Purpose:** Migrate all JSON files from WebhookData/ to database

**Response:**
```json
{
  "migrationDate": "2025-11-14T20:22:42.86047Z",
  "summary": {
    "totalFiles": 15,
    "successful": 15,
    "failed": 0,
    "skipped": 0
  },
  "successfulFiles": [
    {
      "fileName": "messaging_20251114_194201_034.json",
      "fullPath": "WebhookData/messaging_20251114_194201_034.json"
    }
  ],
  "failedFiles": [],
  "skippedFiles": []
}
```

**Processing:**
1. Scans `WebhookData/` directory
2. Reads each JSON file
3. Checks for duplicates in database (by file hash)
4. Inserts new events into database
5. Generates migration report in `Migrations/`

**Code Reference:** `MigrationController.cs:28`

---

##### GET /api/Migration/database-stats

**Purpose:** Get current database statistics

**Response:**
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

**Code Reference:** `MigrationController.cs:48`

---

### 3. HomeController.cs

**Location:** `Controllers/HomeController.cs`
**Route:** `/`
**Purpose:** Default endpoint and health check

#### Endpoints

##### GET /

**Purpose:** Welcome page and service status

**Response:**
```json
{
  "message": "Meta Messenger Webhook Server is running!",
  "endpoints": {
    "webhook": "/webhook",
    "swagger": "/swagger",
    "migration": "/api/Migration"
  }
}
```

**Code Reference:** `HomeController.cs:12`

---

## Request Flow

### Webhook Processing Flow

```
┌─────────────────────────────────────────────────────────────┐
│                   1. HTTP POST /webhook                      │
│                  (Meta sends webhook event)                  │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│          2. WebhookController.ReceiveWebhook()              │
│   - Enable request buffering                                 │
│   - Read raw body as string                                  │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│           3. Extract X-Hub-Signature-256 Header             │
│   - Check X-Hub-Signature-256 (preferred)                   │
│   - Fallback to X-Hub-Signature (SHA1)                      │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│        4. SignatureValidator.ValidateSignature()            │
│   - Parse signature format (sha256=... or sha1=...)         │
│   - Compute HMAC using App Secret                           │
│   - Constant-time comparison                                │
└────────────────────┬────────────────────────────────────────┘
                     │
           ┌─────────┴──────────┐
           ▼                    ▼
   ┌──────────────┐     ┌──────────────┐
   │  Valid ✓     │     │  Invalid ✗   │
   └──────┬───────┘     └──────┬───────┘
          │                    │
          ▼                    ▼
   ┌──────────────┐     ┌──────────────┐
   │ Continue     │     │ Return 403   │
   │ Processing   │     │ Forbidden    │
   └──────┬───────┘     └──────────────┘
          │
          ▼
┌─────────────────────────────────────────────────────────────┐
│      5. WebhookService.ProcessWebhookEventAsync()           │
│   - Parse JSON body                                          │
│   - Extract messaging events                                 │
│   - Extract sender, recipient, message details              │
└────────────────────┬────────────────────────────────────────┘
                     │
          ┌──────────┴─────────────┐
          ▼                        ▼
┌──────────────────────┐  ┌──────────────────────┐
│  6a. Save to JSON    │  │ 6b. Save to Database │
│  (WebhookStorage)    │  │ (WebhookDatabase)    │
│  - Timestamped file  │  │ - WebhookEvents      │
│  - Backup/audit      │  │ - Messages           │
└──────────────────────┘  └──────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│               7. Return 200 OK {"status": "received"}       │
└─────────────────────────────────────────────────────────────┘
```

---

## Error Handling

### Common HTTP Status Codes

| Status Code | Scenario | Response |
|-------------|----------|----------|
| 200 OK | Webhook processed successfully | `{"status": "received"}` |
| 400 Bad Request | Missing signature header | `{"error": "Missing signature header"}` |
| 403 Forbidden | Invalid signature | `{"error": "Invalid signature"}` |
| 403 Forbidden | Invalid verify token | (Empty response) |
| 500 Internal Server Error | Unhandled exception | `{"error": "Internal server error"}` |

### Error Logging

All errors are logged using ASP.NET Core's `ILogger`:

```csharp
_logger.LogError("Signature validation failed");
_logger.LogWarning("Missing signature header");
_logger.LogInformation("✓ Webhook processed successfully");
```

View logs:
```bash
dotnet run --urls "http://localhost:5234"
```

---

## Examples

### Example 1: Successful Webhook Processing

**Request:**
```http
POST /webhook HTTP/1.1
Host: your-server.com
Content-Type: application/json
X-Hub-Signature-256: sha256=5d41402abc4b2a76b9719d911017c592

{
  "object": "page",
  "entry": [{
    "id": "123456789",
    "time": 1234567890,
    "messaging": [{
      "sender": {"id": "USER123"},
      "recipient": {"id": "PAGE123"},
      "timestamp": 1234567890,
      "message": {
        "mid": "mid.123",
        "text": "Hello!"
      }
    }]
  }]
}
```

**Response:**
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"status": "received"}
```

**Logs:**
```
info: BizFirstMetaMessenger.Controllers.WebhookController[0]
      === Webhook Event Received ===
info: BizFirstMetaMessenger.Controllers.WebhookController[0]
      Payload size: 234 bytes
info: BizFirstMetaMessenger.Controllers.WebhookController[0]
      ✓ Signature validated
info: BizFirstMetaMessenger.Services.WebhookService[0]
      ✓ Saved to: WebhookData/webhook_20251114_194201_017.json
info: BizFirstMetaMessenger.Services.WebhookDatabaseService[0]
      ✓ Saved webhook event ID 1 to database
info: BizFirstMetaMessenger.Controllers.WebhookController[0]
      ✓ Webhook processed successfully
```

---

### Example 2: Invalid Signature

**Request:**
```http
POST /webhook HTTP/1.1
Host: your-server.com
Content-Type: application/json
X-Hub-Signature-256: sha256=invalid_signature_here

{
  "object": "page",
  "entry": []
}
```

**Response:**
```http
HTTP/1.1 403 Forbidden
Content-Type: application/json

{"error": "Invalid signature"}
```

**Logs:**
```
warn: BizFirstMetaMessenger.Services.SignatureValidator[0]
      Signature validation failed
warn: BizFirstMetaMessenger.Controllers.WebhookController[0]
      ✗ Invalid signature
```

---

### Example 3: Webhook Verification

**Request:**
```http
GET /webhook?hub.mode=subscribe&hub.verify_token=bizfirst_webhook_verify_token_2024&hub.challenge=test_challenge_12345 HTTP/1.1
Host: your-server.com
```

**Response:**
```http
HTTP/1.1 200 OK
Content-Type: text/plain

test_challenge_12345
```

**Logs:**
```
info: BizFirstMetaMessenger.Controllers.WebhookController[0]
      === Webhook Verification Request ===
info: BizFirstMetaMessenger.Controllers.WebhookController[0]
      Mode: subscribe, Token: bizfirst_webhook_verify_token_2024, Challenge: test_challenge_12345
info: BizFirstMetaMessenger.Controllers.WebhookController[0]
      ✓ Verification successful
```

---

### Example 4: Migration

**Request:**
```http
POST /api/Migration/migrate-json-files HTTP/1.1
Host: your-server.com
```

**Response:**
```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "migrationDate": "2025-11-14T20:22:42Z",
  "summary": {
    "totalFiles": 15,
    "successful": 0,
    "failed": 0,
    "skipped": 15
  },
  "skippedFiles": [...]
}
```

---

## Testing Controllers

### Unit Tests

See `UnitTests/WebhookControllerTests.cs` for comprehensive controller tests.

**Run tests:**
```bash
cd UnitTests
dotnet test --filter FullyQualifiedName~WebhookControllerTests
```

### Manual Testing

**Using curl:**
```bash
# Test webhook verification
curl "http://localhost:5234/webhook?hub.mode=subscribe&hub.verify_token=your_token&hub.challenge=test123"

# Test webhook (development endpoint - no signature)
curl -X POST http://localhost:5234/webhook/test \
  -H "Content-Type: application/json" \
  -d '{"object":"page","entry":[]}'

# Trigger migration
curl -X POST http://localhost:5234/api/Migration/migrate-json-files

# Get database stats
curl http://localhost:5234/api/Migration/database-stats
```

**Using Swagger UI:**
```
http://localhost:5234/swagger
```

---

## Security Notes

### Signature Validation

All production webhooks **MUST** have valid HMAC signatures:
- Signature is computed using the App Secret
- Uses constant-time comparison to prevent timing attacks
- Supports both SHA256 (preferred) and SHA1 (legacy)

### Test Endpoint Security

⚠️ **The `/webhook/test` endpoint bypasses signature validation**

**Recommendations:**
1. Disable in production (environment check)
2. Use IP whitelisting if needed in staging
3. Monitor access logs for abuse

### Configuration Security

- Store `AppSecret` in environment variables
- Use secure configuration providers (Azure Key Vault, AWS Secrets Manager)
- Never commit secrets to version control

---

## Related Documentation

- [Main README](../README.md)
- [Services Documentation](../Services/README.md)
- [Models Documentation](../Models/README.md)
- [Testing Guide](../UnitTests/README.md)

---

**[← Back to Main README](../README.md)**
