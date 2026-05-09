<!--
Copyright 2026 Julien Bombled

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
-->

# Win11Forge API Documentation

## Overview
Win11Forge exposes a local REST API for GUI integration and automation.

Default URL: `http://localhost:5170/`

Authentication:
- Header: `X-API-Key`
- CSRF header for mutating calls: `X-CSRF-Token`

## Core Endpoints
- `GET /api/version`: Returns the framework display version (`YYYYMMDDxx`, sourced from `Config/version.json`).
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
