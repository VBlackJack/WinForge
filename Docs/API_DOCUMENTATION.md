# Win11Forge API Documentation

## Overview
Win11Forge exposes a local REST API for GUI integration and automation.

Default URL: `http://localhost:5170/`

Authentication:
- Header: `X-API-Key`
- CSRF header for mutating calls: `X-CSRF-Token`

## Core Endpoints
- `GET /api/version`: Returns framework version.
- `GET /api/profiles`: Lists available deployment profiles.
- `GET /api/applications`: Lists application catalog entries.
- `GET /api/status`: Returns API/server status.
- `GET /api/cache/stats`: Returns cache statistics.
- `GET /api/csrf-token`: Returns a CSRF token for the current API key.
- `POST /api/deploy`: Starts a deployment.
- `POST /api/rollback`: Starts a rollback.

## Security Defaults
- Local binding by default.
- API key auth enabled.
- CSRF protection enabled for state-changing endpoints.
- Rate limiting enabled.

## Notes
- Configure API behavior in `Config/api-settings.json`.
- Manage secure API keys through `Core/RestApiServer.psm1` cmdlets (DPAPI-backed storage).
