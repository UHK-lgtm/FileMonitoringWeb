# Database contract

Which tables and columns each project uses in the shared database. Keep this up to date in the same change as any schema change (see "Shared database" in `../CLAUDE.md`).

Projects:

- **Web**: this repo, ASP.NET Core API (`Server/`).
- **FileScan**: `../FileScan`, Azure Function app (`FileScannerFunctionApp/`).

## Columns added by 003

- `FileScan.MonitoringDefinition.MatchType` (`Wildcard` or `Regex`). The scanner does not read it yet (ISSUES G-9).
- `FileScan.MonitoringDefinition.DaysOfWeek` (ISO weekday list, such as `1,2,3,4,5`; NULL = every day). The scanner does not read it yet (ISSUES G-9).
- `FileScan.ExpectedDelivery.MatchType` (snapshot). Written by the planning step once the scanner is updated.

Both apps now use these columns: the scanner reads `MatchType` and `DaysOfWeek` when it plans and matches deliveries, and the web app reads, writes and audits them. The imported flows from script 004 can be enabled once `003` and the new code are deployed.

## Tables in use

### `FileScan.MonitoringDefinition`

Created by `001_create_monitoring_definition.sql`.

| Column | Type | Web | FileScan |
| --- | --- | --- | --- |
| `Id` | int, identity key | read | read |
| `Name` | nvarchar(200) | read | read |
| `RelativePath` | nvarchar(400) | read | read |
| `FileNamePattern` | nvarchar(260) | read | read |
| `IsEnabled` | bit | read | read (filter) |
| `CreatedUtc` | datetime2(3) | not used | not used |
| `UpdatedUtc` | datetime2(3) | not used | not used |

Queries:

- **Web** (`Server/Data/MonitoringDefinitionRepository.cs`): `SELECT Id, Name, RelativePath, FileNamePattern, IsEnabled ... ORDER BY Id`, all rows.
- **FileScan** (`FileScannerFunctionApp/MonitoringDefinitionRepository.cs`): `SELECT Id, Name, RelativePath, FileNamePattern ... WHERE Id = @Id AND IsEnabled = 1`.

### `FileScan.MonitoringDefinition`: columns added by 002 (not yet used by either app)

| Column | Type | Meaning |
| --- | --- | --- |
| `Provider` | nvarchar(100) | Provider name, default `''` |
| `ExpectedFileCount` | int | Files expected per delivery, default 1 |
| `Frequency` | nvarchar(20) | `Daily`, `Weekly`, `EveryNDays`, `Monthly` or `Quarterly` |
| `DayOfWeek` | tinyint | Weekly: 1 = Monday ... 7 = Sunday (ISO) |
| `IntervalDays` | int | EveryNDays: N |
| `AnchorDate` | date | EveryNDays: the date the cycle counts from |
| `DayOfMonth` | tinyint | Monthly and Quarterly: 1-31. 31 means the last day when the month is shorter (scanner rule) |
| `MonthOfQuarter` | tinyint | Quarterly: 1-3, counted from the quarter's first month (Jan/Apr/Jul/Oct) |
| `ExpectedDeliveryTime` | time(0) | Local expected delivery time, default `09:00:00` |
| `BufferMinutes` | int | Tolerance added to the expected time, 0-1440, default 0 |
| `TimeZoneId` | nvarchar(64) | IANA time zone, default `Europe/Luxembourg` |
| `FilenameDateMode` | nvarchar(20) | `NoDate`, `SameDay`, `PreviousDay` or `MonthYear`, default `NoDate` |
| `FilenameDateFormat` | nvarchar(30) | .NET date format such as `yyyyMMdd`, required unless `NoDate` |
| `SizeCheckEnabled` | bit | Turns the size check on, default 0 |
| `ReferenceSizeBytes` | bigint | Expected file size when the size check is on |
| `SizeTolerancePercent` | decimal(5,2) | Allowed deviation from the reference size when the check is on |
| `IsRetired` | bit | Soft delete. A retired row is also disabled (`IsEnabled = 0`) |
| `RetiredAtUtc` | datetime2(3) | When it was retired |
| `RetiredBy` | nvarchar(256) | Who retired it |
| `ConcurrencyToken` | rowversion | Changes on every update, for optimistic concurrency in the web app |

Status values stored in the database are `Pending`, `OnTime`, `Missing`, `Incomplete` and `Late`. The UI shows "On Time". `Not Expected` is never stored (spec section 8).

### `FileScan.MonitoringDefinitionAudit`: written by Web

One row per changed field. Columns: `AuditId`, `MonitoringDefinitionId`, `ChangedAtUtc`, `ChangedBy`, `FieldName`, `OldValue`, `NewValue`. Creation and retirement are recorded with `FieldName` `(created)` and `IsRetired`. The web API must write these rows for every change it saves.

### `FileScan.ExpectedDelivery`: written by FileScan

The production run (`ProductionScan` timer, planning step) inserts one row per scheduled definition and date, and the first `Pending` row in `DeliveryStatusTransition` with it. The Function App's database user needs `INSERT` on both tables.

The production run (`ProductionScan` timer, evidence step) reads open deliveries, updates `ExpectedDelivery` (received count, status, completion time), and writes `FileObservation`, `DeliveryStatusTransition` and `DeliveryWarning`. The Function App's user needs `SELECT`, `INSERT` and `UPDATE` on `ExpectedDelivery`, and `INSERT` on the other three tables. Script `002` does not grant any of these, so add the grants for the Function App's user before the first run.

One row per definition and expected date (unique on `MonitoringDefinitionId`, `ExpectedDate`). It holds the effective values that applied when the delivery was evaluated (`RelativePath`, `FileNamePattern`, `ExpectedDeliveryTime`, `BufferMinutes`, `TimeZoneId`, `DeadlineUtc`, `ExpectedFileCount`) and the current state (`ReceivedFileCount`, `CurrentStatus`, `StatusChangedUtc`, `CompletedAtUtc`, `LastScannedUtc`).

### `FileScan.DeliveryStatusTransition`: written by FileScan, append only

Significant status changes, for example `Incomplete` at the deadline and then `Late`. Unique on `ExpectedDeliveryId`, `ToStatus`, `OccurredAtUtc`. Columns: `FromStatus` (NULL for the first status), `ToStatus`, `OccurredAtUtc`, `RecordedAtUtc`, `ExpectedFileCount`, `ReceivedFileCount`, `DeadlineUtc`.

### `FileScan.FileObservation`: written by FileScan, append only

File evidence. A row is added only when a file's size or timestamps change. Unique on `ExpectedDeliveryId`, `FileName`, `SizeBytes`, `FileCreatedUtc`, `FileLastModifiedUtc`. `IsUnexpected = 1` marks files beyond the expected count.

### `FileScan.DeliveryWarning`: written by FileScan

Warnings, separate from delivery status. `WarningType` is `SizeWarning` (per file) or `UnexpectedFileWarning` (per delivery, `FileName` empty). Unique on `ExpectedDeliveryId`, `WarningType`, `FileName`. `ResolvedAtUtc` is set when the condition no longer applies.

### `FileScan.IncidentComment`: written by Web, append only

Comments and reasons on a delivery. Columns: `CommentId`, `ExpectedDeliveryId`, `CommentText`, `CreatedBy`, `CreatedAtUtc`. FileScan never writes this table.

### `FileScan.ScanRun`: written by FileScan, read by Web

Created by `005_create_scan_run.sql`. One row per production run. Columns: `ScanRunId`, `StartedUtc`, `FinishedUtc` (NULL while running or if the run was abandoned), `Status` (`Running`, `Succeeded`, `Failed`), `TriggerType` (`Timer`, `Manual` reserved), `ScheduledToday`, `OpenDeliveries`, `StatusChanges`, `FailureStage` (`Planning` or `Evidence`), `FailureType` (exception type name only), `InvocationId`.

The web app shows the latest run only, and never returns `FailureStage` or `FailureType`. The Function App writes the row, and a failure to write it is logged without stopping the scan.

## Data Health grants (script 006)

The web app reads the approved sources and the controls, and counts rows in each approved source. FileScan does not use these tables.

- Web App user: `SELECT` on `FileScan.DataHealthSource`, `FileScan.DataHealthControl` and `FileScan.DataHealthControlAudit`. `INSERT` and `UPDATE` on `FileScan.DataHealthControl`. `INSERT` on `FileScan.DataHealthControlAudit`.
- Web App user, for every approved source: `SELECT` on that table or view. Grant these one by one, when an admin adds the source. The check runs against the catalog before any count, so a missing grant shows as an Error row, not a crash.

## Grants needed (not in the scripts)

Scripts `002` and `005` do not grant permissions. Grant these before the first run, to the users the apps connect as.

Web App user:

- `SELECT`, `INSERT`, `UPDATE` on `FileScan.MonitoringDefinition`
- `INSERT` on `FileScan.MonitoringDefinitionAudit` and `FileScan.IncidentComment`
- `SELECT` on `FileScan.ExpectedDelivery`, `FileScan.DeliveryStatusTransition`, `FileScan.FileObservation`, `FileScan.DeliveryWarning` and `FileScan.ScanRun`

Function App user:

- `SELECT` on `FileScan.MonitoringDefinition`
- `SELECT`, `INSERT`, `UPDATE` on `FileScan.ExpectedDelivery`
- `INSERT` on `FileScan.DeliveryStatusTransition`, `FileScan.FileObservation` and `FileScan.DeliveryWarning`
- `INSERT`, `UPDATE` on `FileScan.ScanRun`

## Ownership summary

| Table | Written by | Read by |
| --- | --- | --- |
| `MonitoringDefinition` | Web | Web, FileScan |
| `MonitoringDefinitionAudit` | Web | Web |
| `ExpectedDelivery` | FileScan | Web, FileScan |
| `DeliveryStatusTransition` | FileScan | Web |
| `FileObservation` | FileScan | Web |
| `DeliveryWarning` | FileScan | Web |
| `IncidentComment` | Web | Web |
| `ScanRun` | FileScan | Web |

Both projects read the tables that the web app shows. Any change to a table in this list follows the rules in `../CLAUDE.md`, "Shared database".
