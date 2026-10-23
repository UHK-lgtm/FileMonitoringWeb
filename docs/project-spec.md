# File Monitoring Web App - Initial Project Context and Azure Setup
 
## 1. Purpose
 
This project is the web application for the File Monitoring solution.
 
The immediate goal is NOT to build the complete monitoring product. The first milestone is to prove the web application infrastructure end-to-end:
 
```text
User
  -> Microsoft Entra ID sign-in
  -> Azure Web App
  -> React frontend
  -> ASP.NET Core backend API
  -> existing SQL Server database
  -> read a simple table
  -> display the rows in React
```
 
Once this foundation works in Azure, the application can be expanded into the configuration and monitoring UI.
 
---
 
 
## Repository Decision
 
The Web App will be developed in a **new, separate Git repository** from the existing FileScanner Azure Function repository.
 
This is an intentional source-control boundary only. The Web App and FileScanner remain parts of the same File Monitoring solution and will share the existing SQL Server/database contract where appropriate.
 
Recommended new repository structure:
 
```text
FileMonitoringWeb/
|
+-- Server/
|   +-- ASP.NET Core backend API
|   +-- Data/
|   +-- Models/
|   +-- Program.cs
|
+-- ClientApp/
|   +-- React + TypeScript
|   +-- src/
|   +-- package.json
|   +-- vite.config.ts
|
+-- docs/
+-- README.md
+-- CLAUDE.md
```
 
Deployment remains **one Azure Web App** for the Web project: the ASP.NET Core backend serves the compiled React frontend and exposes `/api/...` endpoints.
 
Do not move or duplicate the FileScanner Function code into this repository. Do not couple Web App deployment to Function App deployment.
 
The shared integration boundary is SQL, initially `filescan.MonitoringDefinition`. Any future database-contract changes that affect both repositories must be documented/coordinated explicitly.
 
---
 
## 2. Deployment Architecture
 
Use one Azure Web App / App Service deployment for both frontend and backend initially.
 
```text
                 Microsoft Entra ID
                        |
                        v
                Azure Web App
          +---------------------------+
          |                           |
          | React frontend            |
          |        |                  |
          |        v                  |
          | ASP.NET Core backend API  |
          |                           |
          +-------------+-------------+
                        |
                        v
                 SQL Server VM
```
 
The React frontend and ASP.NET Core backend should remain logically separated in the source code, but they do not need separate Azure Web Apps for this initial/simple internal application.
 
The ASP.NET Core application can serve the production React build as static files and expose backend endpoints under `/api/...`.
 
Advantages for this project:
 
- one Azure Web App to deploy and manage
- one public application URL
- simpler Entra ID setup
- frontend and API use the same origin
- no unnecessary cross-origin/CORS architecture
- fewer Azure resources
- simpler deployment and troubleshooting
 
Do not split frontend and backend into separate Azure applications unless a future requirement justifies it.
 
---
 
## 3. Technology Stack
 
Frontend:
 
- React
- TypeScript preferred
- Vite preferred for the React build
 
Backend:
 
- ASP.NET Core
- .NET 10 where supported by the selected Azure App Service runtime/deployment approach
- `Microsoft.Data.SqlClient` for the first SQL connectivity/read test
 
Authentication:
 
- Microsoft Entra ID
 
Hosting:
 
- Azure App Service / Azure Web App
- one deployment containing ASP.NET Core + compiled React static assets
 
Database:
 
- existing SQL Server running on a VM
- existing database used by FileScanner
 
---
 
## 4. Existing Database Context
 
The FileScanner Azure Function already successfully connects to the existing SQL Server VM.
 
A configuration table already exists:
 
```text
filescan.MonitoringDefinition
```
 
Current known fields include:
 
- `Id`
- `Name`
- `RelativePath`
- `FileNamePattern`
- `IsEnabled`
- `CreatedUtc`
- `UpdatedUtc`
 
The web application's first SQL test should be READ ONLY.
 
Do not create CRUD functionality yet.
 
Do not redesign the FileScanner table just to support the first web test.
 
---
 
## 5. First Web Application Milestone
 
Build the smallest useful vertical slice:
 
```text
Authenticated user
      |
      v
React application
      |
      v
GET /api/monitoring-definitions
      |
      v
ASP.NET Core API
      |
      v
SQL Server
      |
      v
filescan.MonitoringDefinition
      |
      v
JSON response
      |
      v
React table
```
 
Acceptance criteria:
 
1. The application builds locally.
2. React loads successfully.
3. The ASP.NET Core API runs successfully.
4. A backend endpoint can read `filescan.MonitoringDefinition`.
5. React displays the returned records in a simple HTML/table component.
6. The combined application deploys to one Azure Web App.
7. Users must authenticate with Microsoft Entra ID before accessing the application.
8. The deployed backend can reach the existing SQL Server VM.
9. The SQL table is successfully rendered in the deployed React UI.
 
---
 
## 6. Initial UI
 
Keep the first screen deliberately simple.
 
Suggested page:
 
```text
File Monitoring
 
Monitoring Definitions
 
+----+--------------------------+----------------------------------------+----------------------------+---------+
| Id | Name                     | Relative Path                          | File Pattern               | Enabled |
+----+--------------------------+----------------------------------------+----------------------------+---------+
| 1  | BNP Fund Accounting Test| BNP_FUND_ACCOUNTING/IN/PRD/test       | LU_PO_FIO_20260112.xlsx    | Yes     |
+----+--------------------------+----------------------------------------+----------------------------+---------+
```
 
For the first milestone, there is no need for:
 
- create form
- edit form
- delete button
- dashboard charts
- delivery status calculation
- notifications
- admin pages
 
The goal is only to prove authenticated Web App -> API -> SQL -> React rendering.
 
---
 
## 7. Initial Backend API
 
Suggested endpoint:
 
```http
GET /api/monitoring-definitions
```
 
It should return only fields required by the simple table.
 
Example response:
 
```json
[
  {
    "id": 1,
    "name": "BNP Fund Accounting Test",
    "relativePath": "BNP_FUND_ACCOUNTING/IN/PRD/test",
    "fileNamePattern": "LU_PO_FIO_20260112.xlsx",
    "isEnabled": true
  }
]
```
 
Use parameterized SQL for queries requiring parameters.
 
Do not return:
 
- SQL server details
- SQL username/password
- connection strings
- authentication secrets
- tokens
 
---
 
## 8. Database Configuration
 
The Web App will require its own Azure configuration for connecting to the same SQL Server/database.
 
Do not copy secrets into source control.
 
Use Azure App Service configuration/environment settings for the connection string.
 
A configuration key such as `DefaultConnection` / the standard .NET connection-string configuration structure may be used consistently with the backend implementation.
 
Before building business features, independently prove:
 
```text
Azure Web App
     -> networking/VNet
     -> SQL Server VM
     -> open connection
     -> SELECT/read MonitoringDefinition
     -> SUCCESS
```
 
The Web App may require VNet integration/private networking appropriate to the existing SQL VM network. Configure that without changing the already-working FileScanner infrastructure.
 
---
 
## 9. Microsoft Entra ID Authentication
 
The deployed Web App must require organizational Microsoft Entra ID authentication.
 
Initial authentication objective:
 
```text
Unauthenticated browser
       -> application
       -> Entra ID sign-in
       -> authenticated session
       -> React application
```
 
For the first milestone:
 
- require authentication for the application/API
- use the organization's Entra tenant
- do not implement custom username/password authentication
- do not store passwords in the application database
- do not build application roles unless required for the initial deployment
 
Authorization roles such as Viewer/Admin can be introduced later when configuration CRUD is implemented.
 
Keep secrets/certificates out of source control.
 
---
 
## 10. Suggested Source Structure
 
This repository contains only the Web App. Keep its frontend and backend separated logically while deploying them together.
 
Example:
 
```text
FileMonitoringWeb/
|
+-- src/ or Server/
|   +-- ASP.NET Core backend
|   +-- Controllers/ or Endpoints/
|   +-- Data/
|   +-- Models/
|   +-- Program.cs
|
+-- ClientApp/
|   +-- React + TypeScript
|   +-- src/
|   +-- package.json
|   +-- vite.config.ts
|
+-- README.md
+-- CLAUDE.md
```
 
Exact naming can follow the chosen ASP.NET/React template and existing repository conventions.
 
Do not create unnecessary microservices or additional projects for the first milestone.
 
---
 
## 11. Development Sequence
 
### Step 1 - Create the solution
 
Create the new Web App repository, then create the ASP.NET Core backend and React/TypeScript frontend inside it.
 
### Step 2 - Local frontend/backend test
 
Prove:
 
```text
React -> ASP.NET Core API -> simple test response
```
 
### Step 3 - SQL read
 
Add SQL configuration and a read-only repository/service using `Microsoft.Data.SqlClient`.
 
Prove locally/from an environment with SQL access:
 
```text
ASP.NET API -> filescan.MonitoringDefinition -> JSON
```
 
### Step 4 - Render the table
 
React calls:
 
```text
GET /api/monitoring-definitions
```
 
and renders the result.
 
### Step 5 - Create Azure Web App
 
Create one Azure App Service/Web App for the combined application.
 
### Step 6 - Configure networking
 
Configure the Web App so the ASP.NET backend can reach the existing SQL Server VM privately.
 
Do not expose SQL publicly just to make the Web App work.
 
### Step 7 - Configure Entra ID
 
Enable/configure Microsoft Entra ID authentication for the deployed application.
 
### Step 8 - Deploy
 
Deploy ASP.NET Core together with the compiled React application.
 
### Step 9 - Azure verification
 
Verify the full chain:
 
```text
Entra sign-in
    -> Azure Web App
    -> React
    -> ASP.NET API
    -> SQL Server VM
    -> MonitoringDefinition
    -> React table
    -> SUCCESS
```
 
---
 
## 12. Security Requirements
 
Never commit or expose:
 
- SQL passwords
- complete connection strings
- Entra client secrets
- bearer/access tokens
- Azure credentials
- Function keys
- Authorization headers
 
Use Azure configuration/environment settings for secrets.
 
SQL queries must not be constructed from untrusted user input through string concatenation.
 
The initial page is read-only, which intentionally reduces risk while infrastructure is being proven.
 
---
 
## 13. Out of Scope for the First Milestone
 
Do NOT implement yet:
 
- MonitoringDefinition create/edit/delete
- complete configuration screens
- monitoring results dashboard
- scanner orchestration
- direct calls from React to FileScanner
- delivery schedule configuration
- Pending/On Time/Missing/Incomplete/Late status rules (see `docs/File_Monitoring_V1_Specifications.md`, section 10)
- notifications
- email alerts
- file content processing
- complex authorization roles
- multiple Azure Web Apps
- microservices
 
First prove the web infrastructure.
 
---
 
## 14. Future Direction
 
After the first Web App milestone passes, the next likely stage will be configuration management:
 
```text
MonitoringDefinition
 
List      -> first milestone
Create    -> later
Edit      -> later
Enable    -> later
Disable   -> later
Delete/archive -> later
```
 
The database remains the integration boundary between configuration management and the FileScanner worker.
 
Do not make the frontend directly responsible for Azure File Share access or scanning.
 
---
 
## 15. Definition of Done for This Web Milestone
 
This milestone is complete only when the deployed Azure environment proves:
 
```text
User
 |
 v
Microsoft Entra ID
 |
 v
Azure Web App
 |
 +-- React frontend
 |
 +-- ASP.NET Core API
          |
          v
     SQL Server VM
          |
          v
filescan.MonitoringDefinition
          |
          v
     React table
          |
          v
       SUCCESS
```
 
At that point, record the Azure configuration and verified result in the project documentation before adding CRUD or other business functionality.
 