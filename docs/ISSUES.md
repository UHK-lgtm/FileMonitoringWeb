# Issues, decisions and gaps

The running log for the V1 build and the first deployment. Add to it as you test. Each item has an ID, so we can refer to it when we fix it here.

Status values: `Open` (needs work), `Decided` (chosen, with a reason), `Fixed`, `Accepted` (known, not fixed for this release).

## Decisions made without you

| ID | Decision | Why | Where to change |
| --- | --- | --- | --- |
| D-1 | A file counts for an expected delivery only if its last-modified date (in the definition's time zone) is on or after the expected date. Files without a last-modified time are accepted. | Without it, an old file (such as the January test file) makes a later delivery On Time. | `FileScan/FileScannerFunctionApp/DeliveryWindow.cs` |
| D-2 | `{date}` in a file-name pattern is replaced by the date in the mode's format. `PreviousDay` uses the day before the expected date. | The spec does not say how the date fits into the pattern. | `FileScan/FileScannerFunctionApp/FilenameDateRules.cs` |
| D-3 | Default time zone is `Europe/Luxembourg`, and the default delivery time is 09:00 with no buffer. | The spec mentions Luxembourg and the sample deadlines. | `sql/002_create_v1_schema.sql` |
| D-4 | "Today" in the web app is the date in Europe/Luxembourg. | Same zone as the deliveries. | `Server/Domain/LocalDates.cs` |
| D-5 | Writes use the header that App Service adds after Entra sign-in (`X-MS-CLIENT-PRINCIPAL-NAME`) as the user name. Writes without it are refused with 401. | Entra sign-in is already enforced by App Service. | `Server/Endpoints/CurrentUser.cs` |
| D-6 | Edits use an optimistic concurrency token (`rowversion`). A stale save gets 409, and the user is asked to reload. | Two people can edit the same definition. | `Server/Data/MonitoringDefinitionRepository.cs` |
| D-7 | Retire is a soft delete: `IsRetired` true and `IsEnabled` false, with an audit row. There is no hard delete. | The spec forbids hard delete for definitions with history. | `Server/Data/MonitoringDefinitionRepository.cs` |
| D-8 | Excel values that start with `=`, `+`, `-` or `@` are prefixed with an apostrophe. | Otherwise a typed value can run as a formula when the file is opened. | `Server/Export/AuditWorkbook.cs` |
| D-9 | Errors return a fixed message. The log records the exception type only. | SQL messages can contain server names. | `Server/Program.cs`, `FileScan` services |
| D-10 | Status values are stored as `Pending`, `OnTime`, `Missing`, `Incomplete`, `Late`. The UI shows "On Time". | Matches the schema check constraints. | `sql/002_create_v1_schema.sql` |
| D-11 | The web app runs the frontend with no router, using a sidebar and sections in one page. | Fewer moving parts for a deadline. | `ClientApp/src/App.tsx` |
| D-12 | Brand red is a placeholder (`#c8102e`). The look is set by design tokens at the top of the stylesheet. | The company colour was not supplied. | `ClientApp/src/App.css`, `--c-brand` and `--c-brand-dark` |
| D-13 | The Today dashboard summarises the loaded day only. Charts over time are not built, because they need new API endpoints. | Keeps the release on the existing contract. | `ClientApp/src/screens/TodayScreen.tsx` |

## Known gaps (accepted for this release)

| ID | Gap | Impact | Plan |
| --- | --- | --- | --- |
| G-1 | No roles. Every signed-in user can edit definitions and add comments. | Anyone with access can change configuration. | Viewer and Admin roles, with Entra app roles, after this release. The spec defers them. |
| G-2 | "Not Expected" is not shown on the Today screen. | A definition not scheduled today does not appear. | The API needs the schedule rules to report it. |
| G-3 | Size warnings are not raised, only unexpected-file warnings. | The size-check settings are saved but have no effect yet. | Size check in `FileScan` evidence. |
| G-4 | The production timer (`ProductionScan`) runs planning and evidence on the `ScanSchedule` setting. It is built, not yet verified in Azure. | Statuses change on their own once the timer runs, so verify the first run first. | Verify the first run in Azure, then mark this fixed. |
| G-5 | Warnings are never resolved automatically. | A warning stays open after its cause is fixed. | Resolve rule in the evidence run. |
| G-6 | Local testing of the web app cannot pass Entra sign-in. Writes need the `X-MS-CLIENT-PRINCIPAL-NAME` header. | Writes cannot be tested on a laptop without the header. | Test in Azure, or send the header by hand for a local test only. |
| G-7 | Retired definitions stay in the list (greyed) and cannot be enabled. | Lists grow over time. | Filter in the UI. |
| G-8 | The export has two sheets: configuration audit and deliveries. Comments and file evidence are not exported. | The spec asks for more detail in the audit export. | Add sheets once the audit view is confirmed. |

## Open questions for you

| ID | Question | Default in the build |
| --- | --- | --- |
| Q-1 | Is "today" always the Luxembourg date, or should each definition use its own time zone? | Luxembourg, for the whole screen |
| Q-2 | Should a delivery that is complete after its deadline still count as Late even if the provider sends the files later that day? | Yes, per spec section 10 |
| Q-3 | Who may edit definitions before roles exist? | Any signed-in user |

## Deployment and first run checklist

1. **Database permissions.** Script `002` does not grant them. Add these for the Web App's database user:
   - `SELECT`, `INSERT`, `UPDATE` on `FileScan.MonitoringDefinition`
   - `INSERT` on `FileScan.MonitoringDefinitionAudit` and `FileScan.IncidentComment`
   - `SELECT` on `FileScan.ExpectedDelivery`, `FileScan.DeliveryStatusTransition`, `FileScan.FileObservation`, `FileScan.DeliveryWarning`
   Add these for the Function App's database user: see `sql/CONTRACT.md`.
2. **Web App build.** `cd ClientApp && npm ci && npm run build`, then `dotnet publish Server -c Release -o publish`, then deploy.
3. **Smoke checks, in order:**
   - `GET /api/health` returns `ok`.
   - Signed in, `GET /api/monitoring-definitions` returns the BNP row.
   - Signed in, the Today screen shows today's delivery after the planning run.
   - Signed in, the delivery detail opens, and adding a comment shows it on screen.
   - Edit a definition and save. The audit tab shows one row per changed field.
   - Save the same definition from a stale page. It must show the reload message, not overwrite.
   - Export the Excel file. It opens, and no cell starts with a formula.
4. **Log check.** The log shows exception types only, never a connection string or SQL message.

## Issue log

Add a row for each problem found during testing. Fix it here, then mark it `Fixed` with the commit.

| ID | Found | Area | Symptom | Status | Fix |
| --- | --- | --- | --- | --- | --- |
| I-1 | | | | Open | |
