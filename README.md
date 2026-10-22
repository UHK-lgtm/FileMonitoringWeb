# FileMonitoringWeb

Web application for the File Monitoring solution. It is a React frontend served by an ASP.NET Core backend from one Azure Web App. It is part of the File Monitoring system together with FileScan, and it was split out so it can be developed on its own.

The first milestone proves the chain end to end: Microsoft Entra ID sign-in, the ASP.NET Core API, the existing SQL Server database and a React table. See `docs/project-spec.md`.

## Repo layout

- `FileMonitoringWeb.slnx`: solution file
- `Server/`: ASP.NET Core backend API (`/api/...`, serves the React build)
- `ClientApp/`: React + TypeScript frontend, Vite
- `docs/`: project specification and documentation index
- `CLAUDE.md`: guidance for Claude Code working in this repo

## Quick start

Requires .NET 10 SDK and Node.js.

```bash
# Backend (http://localhost:5059)
dotnet run --project Server

# Frontend dev server (proxies /api to the backend), in a second terminal
cd ClientApp
npm ci
npm run dev

# Production build of the React app (output goes to Server/wwwroot)
npm run build
```

Start with `docs/project-spec.md` for scope, architecture and the development sequence.
