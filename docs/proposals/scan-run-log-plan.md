# Plan and test cases: scan-run log and "Last scan" status

Status: **plan and test cases only, pending approval.** No code, script or database change has been made. Read with `scan-run-log.md`, which holds the table design.

## Assumptions

The business owner has not confirmed these. Each one is a recommended default from the product-owner review. Change any of them before build starts.

| Topic | Assumed value | Confirmed? |
| --- | --- | --- |
| Who sees scan status | Every signed-in user, one line only | No |
| Failure type and stage in the UI | Not shown. Stored and logged only | No |
| "Current" | Last successful run is within 15 minutes | No |
| "Stale" | Last successful run is older than 15 minutes | No |
| Stuck run | `Running` for more than 30 minutes shows "Did not finish" | No, measure first |
| No run recorded | "No scan recorded". Never "OK" | No |
| SQL down when a run starts | Scan still runs. Run is not recorded, shows as "Unknown" | No |
| Display timezone | Europe/Luxembourg | No |
| History screen | Not in V1 | No |
| Retention | Keep all rows | No |

## Part A: record each run (FileScan + FileMonitoringWeb SQL)

### Changes

1. **Script `FileMonitoringWeb/sql/005_create_scan_run.sql`.** The number is 005 because 003 and 004 already exist. Idempotent and additive, in the style of `002`.
2. **`FileMonitoringWeb/sql/CONTRACT.md`.** Add the `FileScan.ScanRun` section, with FileScan writing and the web app reading.
3. **`FileScan/FileScannerFunctionApp/ScanRunRepository.cs`** (new). Start, complete and fail a run. Parameterized SQL only.
4. **`FileScan/FileScannerFunctionApp/ScanRunRules.cs`** (new, pure). Maps a run's timestamps and status to the display state. No Azure or SQL types, so it can be unit tested.
5. **`ScanOrchestrator.cs`.** Insert the `Running` row first. Record the step that threw as `Planning` or `Evidence`. Update the row on success or failure. Rethrow failures.
6. **`ProductionScanFunction.cs`.** Pass the Functions invocation ID into the run, so logs can be matched to the row.

### Unit tests (FileScan, `FileScannerFunctionApp.Tests`)

File: `ScanRunRulesTests.cs`

| ID | Given | When | Then |
| --- | --- | --- | --- |
| R1 | Last success 5 minutes ago | Display state computed | `Current` |
| R2 | Last success 20 minutes ago | Display state computed | `Stale` |
| R3 | No runs recorded | Display state computed | `NoRunRecorded` |
| R4 | `Running` started 10 minutes ago | Display state computed | `Running` |
| R5 | `Running` started 45 minutes ago | Display state computed | `DidNotFinish` |
| R6 | Latest run `Failed` at `Evidence` | Display state computed | `Failed`, stage shown in logs only |
| R7 | `StartedUtc` in the future (clock skew) | Display state computed | `Unknown` |
| R8 | Boundary: last success exactly 15 minutes ago | Display state computed | `Current` (inclusive) |

File: `ScanOrchestratorTests.cs` (fake repositories and services)

| ID | Given | When | Then |
| --- | --- | --- | --- |
| O1 | All steps succeed | Run | Row written `Running`, then `Succeeded` with `FinishedUtc` and three counts |
| O2 | Planning throws | Run | Row `Failed`, `FailureStage = Planning`, evidence not called, exception rethrown |
| O3 | Evidence throws | Run | Row `Failed`, `FailureStage = Evidence`, exception rethrown |
| O4 | Start insert throws (SQL down) | Run | Scan still runs; summary returned; failure logged |
| O5 | Exception has a message with a server name | Run fails | Stored `FailureType` is the type name only; message not stored |
| O6 | Complete update throws after success | Run | Logged. **Open decision:** whether this fails the invocation (see below) |

### Integration tests (live database)

The project has no dev copy of the database (`FileMonitoringWeb/CLAUDE.md`). Run these carefully and only after approval.

| ID | Step | Expected |
| --- | --- | --- |
| I1 | Run `005` in SSMS | Table and index created, no errors |
| I2 | Run `005` a second time | No change, no errors |
| I3 | Check constraints | Inserting `Status = 'Bogus'` or `TriggerType = 'Bogus'` is rejected |
| I4 | Deploy FileScan and wait for one timer run | One row with `Status = Succeeded`, counts set, `FinishedUtc` after `StartedUtc` |
| I5 | Existing smoke check | `GET /api/monitoring-definitions` still returns rows; the production run completes with no errors |

## Part B: "Last scan" on Today (FileMonitoringWeb)

Do not start Part B until Part A is deployed and I4 passes.

### Changes

1. **`Server/Models/ScanRun.cs`** and **`Server/Data/ScanRunRepository.cs`.** Read the latest run with a parameterized query.
2. **`Server/Domain/ScanStatusRules.cs`** (new, pure). Same display rules as Part A's R1–R8, kept in the web project because it cannot reference FileScan.
3. **`Server/Endpoints/ScanRunEndpoints.cs`.** `GET /api/scan-runs/latest` only. The history endpoint is out of scope.
4. **`docs/api-contract.md`.** Add the endpoint. **`docs/File_Monitoring_V1_Specifications.md`.** Add "Last scan" to section 27 and the scan-run table to section 28, before building.
5. **`ClientApp/src/api.ts`** and a small `ScanStatusLine` component on `TodayScreen.tsx`.

### Server tests (`Server.Tests`)

| ID | Given | Then |
| --- | --- | --- |
| W1 | Latest run `Succeeded`, 5 minutes old | Response state `Current` |
| W2 | No rows | `NoRunRecorded`, HTTP 200 (not 404) |
| W3 | Response body | Contains no connection string, no exception text, no `FailureType` |
| W4 | Unauthenticated request | Refused by App Service sign-in (manual check, see below) |

### Manual UI checks (no client test framework exists yet)

| ID | Condition | Expected line on Today |
| --- | --- | --- |
| U1 | Recent success | "Last scan: <local time>, current" |
| U2 | Old success | "Last scan: <local time>, stale" in a warning style |
| U3 | No rows | "No scan recorded" |
| U4 | Stuck `Running` | "Did not finish" |
| U5 | Endpoint returns an error | "Scan status unavailable". Delivery statuses still show |
| U6 | Times | Shown in Europe/Luxembourg |

## Rollout order

1. Owner confirms the assumptions and the open decision.
2. Run `005` on the live database. Run the existing smoke checks.
3. Build and test FileScan (`dotnet build`, `dotnet test`). Push FileScan with the CONTRACT change. Deploy. Check I4.
4. Build and test the web app (`dotnet build`, `dotnet test Server.Tests`, `npm run lint`, `npm run build`). Push. Deploy. Check W4 and U1–U6.

## Rollback

- FileScan: redeploy the previous version. The `ScanRun` table can stay, because it is additive. Nothing else reads it.
- Web: redeploy the previous version. Today then shows no scan line, as before.
- The table is never dropped as part of rollback, so recorded history is kept.

## Open decision

- **O6:** if the final "Succeeded" update fails after the scan itself succeeded, should the invocation fail? Recommended: no. The scan result is real and the failure is logged. Confirm.

## Definition of done

- All tests in section "Unit tests", "Server tests" and "Integration tests" pass, or any failure is reported with its output.
- Both projects build before each push.
- The CONTRACT and the spec agree with the code.
- The Today screen shows one of the states above for each condition in U1–U5.
