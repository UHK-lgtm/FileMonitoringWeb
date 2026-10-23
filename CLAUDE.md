# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

FileMonitoringWeb is the web application for the File Monitoring solution. It is a React frontend served by an ASP.NET Core backend from one Azure Web App. The first milestone is an end-to-end proof, not the full product: an authenticated user signs in with Microsoft Entra ID, the API reads the existing `filescan.MonitoringDefinition` table from SQL Server, and React renders the rows.

FileScan (the Azure Function that scans the file share) is a separate repo. This app does not call FileScan directly. The shared boundary is the SQL table.

## Read first

The specification in `docs/` is the source of truth:

- `docs/project-spec.md`: purpose, deployment architecture, first milestone, acceptance criteria and out-of-scope items
- `docs/File_Monitoring_V1_Specifications.md`: the business rules for the whole solution (schedules, statuses, warnings, audit) and what the scanner, API and frontend each own
- `docs/future-features.md`: items outside V1. Do not build them until they are moved into a current spec

FileScan's docs (`FileScan/docs/`) describe the database and the scanner. Read them when the SQL contract matters.

## Layout

- `FileMonitoringWeb.slnx`: solution file (.NET 10 format)
- `Server/`: ASP.NET Core backend API (serves the compiled React app and `/api/...`)
- `ClientApp/`: React + TypeScript frontend (Vite). `npm run build` writes to `Server/wwwroot/`.
- `Server.Tests/`: xUnit tests for the validation and audit rules.
- `sql/`: schema scripts for the shared database. This repo owns them (see "Shared database").
- `docs/`: documentation and project specification

## Shared database

This repo owns the SQL schema for the File Monitoring solution. The FileScan repo (`../FileScan`, `FileScannerFunctionApp/`) reads and writes the same database and depends on it, so the two projects must be kept in step.

- All schema changes are scripts in `sql/`, numbered and idempotent (`002_...sql`, `003_...sql`). Never edit a script that has already been run against a shared database; add a new one.
- Only this repo runs DDL. FileScan and the web app only read and write data.
- Write ownership per table (from `docs/File_Monitoring_V1_Specifications.md`, sections 25–28):
  - Web/API writes: Monitoring Definitions, configuration audit, comments/reasons.
  - FileScan writes: expected-delivery records, current status, file evidence, status transitions, warnings.
- This is a proof of concept with one live database, and no dev copy. Keep both projects in step with these rules:
  1. `sql/CONTRACT.md` lists every table and column each project uses, and whether it reads or writes it. Update it in the same change as any schema change.
  2. A schema change and the code that depends on it go in the same commit, or in commits pushed together. Never push a schema change that the other project depends on without the matching code change.
  3. Before pushing, both projects must build: `dotnet build` in this repo and in `../FileScan` (`dotnet build FileScannerFunctionApp/FileScannerFunctionApp.csproj`).
  4. After deploying a schema change, run two smoke checks: `GET /api/monitoring-definitions` returns rows, and FileScan's test HTTP function loads Id 1 with no errors.
  5. Prefer additive changes: new nullable columns and new tables are safe. Rename or drop in two steps, with the code change between them.
- If a schema change and a spec disagree, ask which is right and update the doc first.

## Rules

- If code and a doc disagree, ask which is right and update the doc. Don't silently diverge.
- The first milestone was read-only. Scope now includes the V1 web app: deliveries and status, delivery comments, and definition configuration with audit and Excel export, as in `docs/api-contract.md`. Roles and hard delete remain out of scope. See `docs/ISSUES.md`.
- Do not build a direct path from the frontend to Azure File Share or to FileScan. The frontend calls only `/api/...`.
- Do not expose SQL Server publicly. The Web App reaches it privately through VNet integration.
- Use parameterized SQL only. Never build queries by concatenating untrusted input.
- Responses must not include SQL server details, credentials, connection strings, tokens or authorization headers.
- No secrets or connection strings in code. Use Entra ID for user auth, Azure App Service configuration for the SQL connection string, and Key Vault only if a secret is unavoidable.
- Never commit `appsettings.Development.json`, `local.settings.json`, `.env` files, keys or certificates. They are git-ignored and denied for reading in `.claude/settings.json`.
- Git: commit and push directly to `main`. No branches or PRs while the owner is the only developer.

## Build and test

Run from the repo root unless noted.

- Tests: `dotnet test Server.Tests/FileMonitoringWeb.Server.Tests.csproj`. Must pass before a push.
- Backend build: `dotnet build FileMonitoringWeb.slnx`
- Backend run: `dotnet run --project Server` (HTTP on `http://localhost:5059`, from `Server/Properties/launchSettings.json`)
- Frontend setup: `cd ClientApp && npm ci`
- Frontend dev server: `cd ClientApp && npm run dev` (proxies `/api` to `http://localhost:5059`; run the backend first)
- Frontend build: `cd ClientApp && npm run build` (output in `Server/wwwroot/`)
- Frontend lint: `cd ClientApp && npm run lint`
- Tests: none yet.
