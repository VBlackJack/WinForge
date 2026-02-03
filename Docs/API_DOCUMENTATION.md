# Win11Forge REST API Documentation

This document describes the REST API provided by Win11Forge for programmatic control of deployments.

## Overview

The Win11Forge REST API allows external tools and scripts to:
- Query deployment profiles and applications
- Start and monitor deployments
- Trigger rollback operations
- Check cache and system status

**Base URL:** `http://localhost:5170/api`

**API Version:** 1.0

## Authentication

All API requests require authentication using an API key.

### API Key Header

Include your API key in the `X-API-Key` header:

```http
GET /api/version HTTP/1.1
Host: localhost:5170
X-API-Key: your-api-key-here
```

### CSRF Protection

State-changing requests (POST, PUT, DELETE) require a CSRF token:

1. First, obtain a CSRF token:
   ```http
   GET /api/csrf-token HTTP/1.1
   X-API-Key: your-api-key-here
   ```

2. Include the token in subsequent requests:
   ```http
   POST /api/deploy HTTP/1.1
   X-API-Key: your-api-key-here
   X-CSRF-Token: your-csrf-token-here
   Content-Type: application/json
   ```

### API Key Management

API keys are managed using the `SecureStorage` module:

```powershell
# Create a new API key
Save-SecureApiKey -KeyId 'automation' -ApiKey 'w11f_your_key' -Permissions @('read', 'deploy')

# List API keys
Get-SecureApiKeys

# Remove an API key
Remove-SecureApiKey -KeyId 'automation'
```

## Rate Limiting

- **Per-IP:** 60 requests/minute, 1000 requests/hour
- **Failed Auth:** Auto-block after 10 failed attempts (60 minute duration)

## Endpoints

### GET /api/version

Returns framework version information.

**Authentication:** Required

**Response:**
```json
{
  "framework": "Win11Forge",
  "version": "3.5.2",
  "apiVersion": "1.0",
  "lastUpdated": "2026-01-28",
  "timestamp": "2026-02-03T10:30:00.000Z"
}
```

---

### GET /api/profiles

Lists available deployment profiles.

**Authentication:** Required

**Response:**
```json
{
  "profiles": [
    {
      "id": "Base",
      "name": "Base",
      "description": "Base profile with essential applications",
      "applicationCount": 30,
      "filePath": "C:\\Win11Forge\\Profiles\\Base.json"
    },
    {
      "id": "Gaming",
      "name": "Gaming",
      "description": "Gaming profile with Steam, Discord, etc.",
      "applicationCount": 39,
      "filePath": "C:\\Win11Forge\\Profiles\\Gaming.json"
    }
  ],
  "count": 5,
  "profilesDirectory": "C:\\Win11Forge\\Profiles"
}
```

---

### GET /api/applications

Returns the application database.

**Authentication:** Required

**Query Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| `category` | string | Filter by category (e.g., "Browser", "Development") |
| `search` | string | Search by name or ID |

**Examples:**
```http
GET /api/applications?category=Browser
GET /api/applications?search=Chrome
```

**Response:**
```json
{
  "applications": [
    {
      "id": "GoogleChrome",
      "name": "Google Chrome",
      "category": "Browser",
      "installMethod": "Winget",
      "description": "Fast, secure web browser"
    }
  ],
  "count": 175,
  "categories": {
    "Browser": 8,
    "Development": 25,
    "Utility": 42
  },
  "databasePath": "C:\\Win11Forge\\Apps\\Database\\applications.json"
}
```

---

### GET /api/status

Returns current deployment status.

**Authentication:** Required

**Response:**
```json
{
  "status": "Running",
  "currentProfile": "Gaming",
  "progress": 45,
  "startTime": "2026-02-03T10:00:00.000Z",
  "uptime": "00:30:15",
  "applicationsProcessed": 18,
  "errors": [],
  "timestamp": "2026-02-03T10:30:15.000Z"
}
```

**Status Values:**
| Status | Description |
|--------|-------------|
| `Idle` | No deployment in progress |
| `Starting` | Deployment is initializing |
| `Running` | Deployment in progress |
| `Completed` | Deployment finished successfully |
| `Failed` | Deployment failed |
| `RollingBack` | Rollback in progress |

---

### POST /api/deploy

Starts a deployment.

**Authentication:** Required

**CSRF Token:** Required

**Request Body:**
```json
{
  "profile": "Gaming",
  "testMode": false
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `profile` | string | Yes | Profile name (max 100 chars, no path characters) |
| `testMode` | boolean | No | If true, simulates deployment without installing |

**Response (Success):**
```json
{
  "success": true,
  "message": "Deployment started for profile: Gaming",
  "profile": "Gaming",
  "testMode": false,
  "startTime": "2026-02-03T10:00:00.000Z"
}
```

**Response (Error - Already Running):**
```json
{
  "success": false,
  "error": "Deployment is already in progress",
  "currentProfile": "Base"
}
```

**Response (Error - Invalid Profile):**
```json
{
  "success": false,
  "error": "Profile not found: InvalidProfile"
}
```

**Response (Error - Security Violation):**
```json
{
  "success": false,
  "error": "Invalid profile name: contains forbidden characters"
}
```

---

### POST /api/rollback

Triggers a rollback operation.

**Authentication:** Required

**CSRF Token:** Required

**Request Body:**
```json
{
  "force": false
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `force` | boolean | No | If true, executes rollback immediately |

**Response (Preview - force=false):**
```json
{
  "success": true,
  "message": "Rollback summary retrieved. Set force=true to execute.",
  "summary": {
    "sessionId": "abc123",
    "totalApps": 5,
    "rollbackableCount": 5,
    "applications": [
      {"name": "VSCode", "method": "Winget"},
      {"name": "Git", "method": "Winget"}
    ]
  }
}
```

**Response (Execute - force=true):**
```json
{
  "success": true,
  "message": "Rollback completed",
  "appsRolledBack": 5,
  "errors": []
}
```

---

### GET /api/cache/stats

Returns cache statistics.

**Authentication:** Required

**Response:**
```json
{
  "winget": {
    "listCacheValid": true,
    "listCacheAgeMinutes": 15,
    "listHits": 42,
    "listMisses": 3,
    "listHitRate": "93.33%",
    "searchCacheEntries": 12,
    "searchHits": 28,
    "searchMisses": 8,
    "searchHitRate": "77.78%",
    "lastWarmup": "2026-02-03T09:45:00.000Z"
  },
  "timestamp": "2026-02-03T10:30:00.000Z"
}
```

---

### GET /api/csrf-token

Generates a CSRF token for state-changing requests.

**Authentication:** Required

**Response:**
```json
{
  "csrfToken": "abc123xyz789...",
  "expiresInMinutes": 60,
  "headerName": "X-CSRF-Token",
  "timestamp": "2026-02-03T10:30:00.000Z"
}
```

## Error Responses

All errors follow a consistent format:

```json
{
  "error": "Error message description",
  "code": "ERROR_CODE",
  "timestamp": "2026-02-03T10:30:00.000Z"
}
```

**Common Error Codes:**
| Code | HTTP Status | Description |
|------|-------------|-------------|
| `UNAUTHORIZED` | 401 | Missing or invalid API key |
| `FORBIDDEN` | 403 | Valid key but insufficient permissions |
| `NOT_FOUND` | 404 | Endpoint or resource not found |
| `VALIDATION_FAILED` | 400 | Request body validation failed |
| `RATE_LIMITED` | 429 | Rate limit exceeded |
| `INTERNAL_ERROR` | 500 | Server-side error |

## Example Workflows

### Deploy a Profile

```powershell
# 1. Get CSRF token
$headers = @{ 'X-API-Key' = 'your-api-key' }
$csrf = Invoke-RestMethod -Uri 'http://localhost:5170/api/csrf-token' -Headers $headers

# 2. Start deployment
$headers['X-CSRF-Token'] = $csrf.csrfToken
$body = @{ profile = 'Gaming' } | ConvertTo-Json
$result = Invoke-RestMethod -Uri 'http://localhost:5170/api/deploy' -Method POST -Headers $headers -Body $body -ContentType 'application/json'

# 3. Monitor progress
do {
    Start-Sleep -Seconds 5
    $status = Invoke-RestMethod -Uri 'http://localhost:5170/api/status' -Headers $headers
    Write-Host "Progress: $($status.progress)%"
} while ($status.status -eq 'Running')
```

### Search Applications

```powershell
$headers = @{ 'X-API-Key' = 'your-api-key' }
$apps = Invoke-RestMethod -Uri 'http://localhost:5170/api/applications?search=Visual%20Studio' -Headers $headers
$apps.applications | Format-Table id, name, category
```

## Security Considerations

1. **API Key Storage:** API keys are encrypted using Windows DPAPI. Never share or commit API keys.

2. **Localhost Only:** By default, the API only accepts connections from localhost.

3. **CSRF Protection:** All state-changing endpoints require CSRF tokens to prevent cross-site request forgery.

4. **Rate Limiting:** Automatic rate limiting prevents abuse and DoS attacks.

5. **Path Traversal Protection:** Profile names are validated to prevent directory traversal attacks.

6. **Input Validation:** All request bodies are validated against JSON schemas.

## Configuration

API settings are stored in `Config/api-settings.json`:

```json
{
  "enabled": true,
  "host": "localhost",
  "port": 5170,
  "localhostOnly": true,
  "requireAuthentication": true,
  "csrfTokenTtlMinutes": 60,
  "rateLimiting": {
    "enabled": true,
    "requestsPerMinute": 60,
    "requestsPerHour": 1000
  }
}
```

## Starting the API Server

```powershell
# Import modules
Import-Module .\Core\RestApiServer.psm1
Import-Module .\Core\ApiEndpoints.psm1

# Register endpoints
Register-DefaultEndpoints

# Start server
Start-ApiServer
```

---

**Author:** Julien Bombled
**License:** Apache 2.0
**Version:** 3.5.2
