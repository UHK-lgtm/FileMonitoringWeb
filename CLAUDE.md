# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

FileMonitoringWeb is the web application for the File Monitoring solution. It is a React frontend served by an ASP.NET Core backend from one Azure Web App. The first milestone is an end-to-end proof, not the full product: an authenticated user signs in with Microsoft Entra ID, the API reads the existing `filescan.MonitoringDefinition` table from SQL Server, and React renders the rows.

FileScan (the Azure Function that scans the file share) is a separate repo. This app does not call FileScan directly. The shared boundary is the SQL table.

## Read first

The specification in `docs/` is the source of truth:

- `docs/project-spec.md`: purpose, deployment architecture, first milestone, acceptance criteria and out-of-scope items

FileScan's docs (`FileScan/docs/`) describe the database and the scanner. Read them when the SQL contract matters.

## Layout

- `FileMonitoringWeb.slnx`: solution file (.NET 10 format)
- `Server/`: ASP.NET Core backend API (serves the compiled React app and `/api/...`)
- `ClientApp/`: React + TypeScript frontend (Vite). `npm run build` writes to `Server/wwwroot/`.
- `docs/`: documentation and project specification

## Rules

- If code and a doc disagree, ask which is right and update the doc. Don't silently diverge.
- The first milestone is read-only. Do not add create, edit or delete features for `MonitoringDefinition` yet.
- Do not build a direct path from the frontend to Azure File Share or to FileScan. The frontend calls only `/api/...`.
- Do not expose SQL Server publicly. The Web App reaches it privately through VNet integration.
- Use parameterized SQL only. Never build queries by concatenating untrusted input.
- Responses must not include SQL server details, credentials, connection strings, tokens or authorization headers.
- No secrets or connection strings in code. Use Entra ID for user auth, Azure App Service configuration for the SQL connection string, and Key Vault only if a secret is unavoidable.
- Never commit `appsettings.Development.json`, `local.settings.json`, `.env` files, keys or certificates. They are git-ignored and denied for reading in `.claude/settings.json`.
- Git: commit and push directly to `main`. No branches or PRs while the owner is the only developer.

## Build and test

Run from the repo root unless noted.

- Backend build: `dotnet build FileMonitoringWeb.slnx`
- Backend run: `dotnet run --project Server` (HTTP on `http://localhost:5059`, from `Server/Properties/launchSettings.json`)
- Frontend setup: `cd ClientApp && npm ci`
- Frontend dev server: `cd ClientApp && npm run dev` (proxies `/api` to `http://localhost:5059`; run the backend first)
- Frontend build: `cd ClientApp && npm run build` (output in `Server/wwwroot/`)
- Frontend lint: `cd ClientApp && npm run lint`
- Tests: none yet.
