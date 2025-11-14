# Migrations

This directory contains migration reports and utilities for migrating existing JSON webhook files to the SQLite database.

---

## Table of Contents

- [Overview](#overview)
- [Migration Process](#migration-process)
- [Migration Reports](#migration-reports)
- [Running Migrations](#running-migrations)
- [Troubleshooting](#troubleshooting)

---

## Overview

The migration system allows you to import existing JSON webhook files from the `WebhookData/` directory into the SQLite database. This is useful when:

- You have existing JSON backup files from before database integration
- You want to consolidate historical webhook data
- You need to analyze all webhook data in a queryable format

---

## Migration Process

### How It Works

```
┌────────────────────────────────────────────────────────┐
│  1. Scan WebhookData/ directory for JSON files         │
└────────────────┬───────────────────────────────────────┘
                 │
                 ▼
┌────────────────────────────────────────────────────────┐
│  2. For each JSON file:                                │
│     - Read file contents                               │
│     - Parse JSON payload                               │
│     - Generate file hash (SHA256)                      │
└────────────────┬───────────────────────────────────────┘
                 │
                 ▼
┌────────────────────────────────────────────────────────┐
│  3. Check if file already migrated                     │
│     - Query database for matching hash                 │
└────────────────┬───────────────────────────────────────┘
                 │
          ┌──────┴──────┐
          ▼             ▼
   ┌──────────┐  ┌──────────┐
   │ Exists   │  │ New      │
   │ Skip     │  │ Insert   │
   └──────────┘  └─────┬────┘
                        │
                        ▼
         ┌──────────────────────────────┐
         │ 4. Save to database:          │
         │    - WebhookEvent table       │
         │    - Messages table (parsed)  │
         └──────────────┬────────────────┘
                        │
                        ▼
         ┌──────────────────────────────┐
         │ 5. Generate migration report  │
         │    - JSON report (detailed)   │
         │    - TXT summary (human)      │
         └───────────────────────────────┘
```

### Features

- **Duplicate Detection** - Uses file path hashing to avoid re-importing
- **Batch Processing** - Processes all files in one operation
- **Detailed Reporting** - Success, failed, and skipped files tracked
- **Error Handling** - Individual file failures don't stop migration
- **Audit Trail** - Complete migration history preserved

---

## Migration Reports

### Report Files

Migration generates two report files in this directory:

1. **JSON Report** - `migration_report_{timestamp}.json`
   - Detailed machine-readable format
   - Complete file lists
   - Used for automated processing

2. **Text Summary** - `migration_summary_{timestamp}.txt`
   - Human-readable format
   - Summary statistics
   - Easy to scan visually

### Report Format

#### JSON Report

```json
{
  "migrationDate": "2025-11-14T20:22:42.86047Z",
  "summary": {
    "totalFiles": 15,
    "successful": 0,
    "failed": 0,
    "skipped": 15
  },
  "successfulFiles": [],
  "failedFiles": [],
  "skippedFiles": [
    {
      "fileName": "messaging_20251114_194201_034.json",
      "fullPath": "WebhookData/messaging_20251114_194201_034.json"
    }
  ]
}
```

#### Text Summary

```
================================================================================
WEBHOOK MIGRATION REPORT
================================================================================

Migration Date: 2025-11-14 20:22:42 UTC

--------------------------------------------------------------------------------
SUMMARY
--------------------------------------------------------------------------------
Total Files Processed: 15
✓ Successfully Migrated: 0
✗ Failed: 0
⊘ Skipped (duplicates): 15

--------------------------------------------------------------------------------
SKIPPED FILES (Already in Database)
--------------------------------------------------------------------------------
  ⊘ field_about_20251114_172838_832.json
  ⊘ messaging_20251114_194201_034.json
  ⊘ messaging_20251114_194321_022.json
  ...

================================================================================
```

### Report Location

All reports are saved in this directory:
```
Migrations/
├── migration_report_20251114_202242.json
├── migration_summary_20251114_202242.txt
├── migration_report_20251115_143052.json
└── migration_summary_20251115_143052.txt
```

---

## Running Migrations

### Method 1: API Endpoint

The easiest way to trigger migration:

```bash
# Start the application
dotnet run --urls "http://localhost:5234"

# Trigger migration
curl -X POST http://localhost:5234/api/Migration/migrate-json-files
```

**Response:**
```json
{
  "migrationDate": "2025-11-14T20:22:42Z",
  "summary": {
    "totalFiles": 15,
    "successful": 15,
    "failed": 0,
    "skipped": 0
  },
  "successfulFiles": [...],
  "failedFiles": [],
  "skippedFiles": []
}
```

### Method 2: Swagger UI

1. Navigate to `http://localhost:5234/swagger`
2. Expand `POST /api/Migration/migrate-json-files`
3. Click **"Try it out"**
4. Click **"Execute"**
5. View migration results

### Method 3: Programmatic

```csharp
var migrationService = new WebhookMigrationService(
    databaseService,
    storageService,
    logger
);

var result = await migrationService.MigrateJsonFilesToDatabaseAsync();

Console.WriteLine($"Successful: {result.Summary.Successful}");
Console.WriteLine($"Failed: {result.Summary.Failed}");
Console.WriteLine($"Skipped: {result.Summary.Skipped}");
```

---

## Migration Statistics

### Check Database Stats

After migration, verify the results:

```bash
curl http://localhost:5234/api/Migration/database-stats
```

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

### Query Database Directly

```bash
sqlite3 bin/Debug/net8.0/webhooks.db

sqlite> SELECT COUNT(*) FROM WebhookEvents;
15

sqlite> SELECT COUNT(*) FROM Messages;
7

sqlite> SELECT EventType, COUNT(*) FROM WebhookEvents GROUP BY EventType;
messaging|8
webhook|7
```

---

## Duplicate Detection

### How It Works

The migration service uses **SHA256 hashing** of the file path to detect duplicates:

```csharp
private string GenerateFileHash(string filePath)
{
    using var sha256 = SHA256.Create();
    var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(filePath));
    return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
}
```

### Why File Path Hash?

- **Unique identifier** - Each file has a unique path
- **Consistent** - Same file always generates same hash
- **Fast** - No need to read file contents
- **Reliable** - Detects if same file is re-migrated

### Checking for Duplicates

```csharp
var fileHash = GenerateFileHash(filePath);
var exists = await _context.WebhookEvents
    .AnyAsync(e => e.RawPayload.Contains(fileHash));

if (exists)
{
    result.SkippedFiles.Add(new FileInfo { ... });
    continue;
}
```

---

## Migration Examples

### Example 1: First Migration

```bash
# Start fresh (15 files in WebhookData/)
$ curl -X POST http://localhost:5234/api/Migration/migrate-json-files

{
  "summary": {
    "totalFiles": 15,
    "successful": 15,
    "failed": 0,
    "skipped": 0
  }
}
```

**Result:** All 15 files imported

---

### Example 2: Re-run Migration

```bash
# Run again (same files)
$ curl -X POST http://localhost:5234/api/Migration/migrate-json-files

{
  "summary": {
    "totalFiles": 15,
    "successful": 0,
    "failed": 0,
    "skipped": 15
  }
}
```

**Result:** All files skipped (already in database)

---

### Example 3: Incremental Migration

```bash
# Add 5 new webhook files to WebhookData/
# Run migration
$ curl -X POST http://localhost:5234/api/Migration/migrate-json-files

{
  "summary": {
    "totalFiles": 20,
    "successful": 5,
    "failed": 0,
    "skipped": 15
  }
}
```

**Result:** 5 new files imported, 15 existing files skipped

---

## Troubleshooting

### Issue 1: Migration Fails with Database Error

**Symptom:**
```json
{
  "summary": {
    "failed": 5
  },
  "failedFiles": [
    {
      "fileName": "webhook_xyz.json",
      "error": "Database constraint violation"
    }
  ]
}
```

**Solutions:**
1. Check database file permissions
2. Verify database file isn't corrupted
3. Check available disk space
4. Review migration logs

---

### Issue 2: Files Not Found

**Symptom:**
```json
{
  "summary": {
    "totalFiles": 0
  }
}
```

**Solutions:**
1. Verify `WebhookData/` directory exists
2. Check file permissions
3. Ensure files have `.json` extension
4. Check `appsettings.json` for correct directory path

---

### Issue 3: All Files Skipped

**Symptom:**
```json
{
  "summary": {
    "totalFiles": 15,
    "skipped": 15
  }
}
```

**Explanation:** This is normal if files were already migrated.

**To Force Re-migration:**
1. Delete database: `rm bin/Debug/net8.0/webhooks.db`
2. Restart application: `dotnet run`
3. Run migration again

---

### Issue 4: JSON Parse Errors

**Symptom:**
```json
{
  "failedFiles": [
    {
      "fileName": "invalid.json",
      "error": "JSON parse error"
    }
  ]
}
```

**Solutions:**
1. Validate JSON files: `cat file.json | jq`
2. Check for corrupted files
3. Verify file encoding (UTF-8)
4. Fix malformed JSON

---

## Best Practices

### 1. Backup Before Migration

```bash
# Backup database before migration
cp bin/Debug/net8.0/webhooks.db bin/Debug/net8.0/webhooks.db.backup

# Backup JSON files
tar -czf WebhookData_backup.tar.gz WebhookData/
```

### 2. Monitor Migration Progress

```bash
# Check logs during migration
dotnet run | grep "Migration"
```

### 3. Verify Results

```bash
# After migration, check stats
curl http://localhost:5234/api/Migration/database-stats

# Review migration report
cat Migrations/migration_summary_*.txt
```

### 4. Clean Up Old Reports

```bash
# Keep recent reports, delete old ones
find Migrations/ -name "migration_*.json" -mtime +30 -delete
find Migrations/ -name "migration_*.txt" -mtime +30 -delete
```

---

## Migration Service Code

The migration service is implemented in:
- **Service:** `Services/WebhookMigrationService.cs`
- **Controller:** `Controllers/MigrationController.cs`

**See:** [Services Documentation](../Services/README.md)

---

## Database Schema

Migrated data is stored in:
- **WebhookEvents table** - Raw webhook events
- **Messages table** - Parsed message data

**See:** [Data Layer Documentation](../Data/README.md)

---

## Related Documentation

- [Main README](../README.md)
- [Services Documentation](../Services/README.md)
- [Data Layer Documentation](../Data/README.md)
- [Controllers Documentation](../Controllers/README.md)

---

**[← Back to Main README](../README.md)**
