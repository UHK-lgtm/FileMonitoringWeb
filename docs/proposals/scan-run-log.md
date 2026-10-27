# Proposal: record each production scan run in SQL

Status: **implemented in code, not yet deployed.** Before the web view shows data, run `sql/005_create_scan_run.sql` on the database and apply the grants in `sql/CONTRACT.md`. The build and test plan is in `scan-run-log-plan.md`.

## Problem

Each `ProductionScan` run is recorded only in the Function's logs. The database cannot answer:

- when the last run started and finished;
- whether the last run succeeded or failed, and at which stage;
- how many deliveries and status changes a run produced.

Per-delivery data exists (`ExpectedDelivery.LastScannedUtc`, `DeliveryStatusTransition`), but a run that fails before reaching a delivery leaves no trace in SQL.

## Proposal

Add one table, `FileScan.ScanRun`. FileScan writes it. The web app may read it later. It is one row per production run.

### Table: `FileScan.ScanRun`

| Column | Type | Null | Meaning |
| --- | --- | --- | --- |
| `ScanRunId` | bigint identity, primary key | no | Row key |
| `StartedUtc` | datetime2(3) | no | When the run started (UTC). Default `SYSUTCDATETIME()`. |
| `FinishedUtc` | datetime2(3) | yes | When the run ended (UTC). Null while running or if the run was abandoned. |
| `Status` | nvarchar(20) | no | `Running`, `Succeeded` or `Failed`. Check constraint. |
| `TriggerType` | nvarchar(20) | no | `Timer` for now. Reserved for `Manual` later. Check constraint. |
| `ScheduledToday` | int | yes | Definitions scheduled today (from `ScanRunSummary`). |
| `OpenDeliveries` | int | yes | Open deliveries assessed in the run. |
| `StatusChanges` | int | yes | Deliveries whose status changed in the run. |
| `FailureStage` | nvarchar(60) | yes | Where a failed run stopped: `Planning`, `Evidence` or `Unknown`. |
| `FailureType` | nvarchar(200) | yes | Exception type name only, for example `SqlException`. **Never** the message, which can contain server names. |
| `InvocationId` | nvarchar(64) | yes | Functions invocation ID, to find the matching log lines. |

Index: `IX_FileScan_ScanRun_StartedUtc` on `StartedUtc DESC`, so "latest runs" is cheap.

Script: `sql/003_create_scan_run.sql`, idempotent, additive, in the same style as `002`.

### Behaviour

1. **Start:** `ProductionScan` inserts a row with `Status = 'Running'` before doing any work.
2. **Success:** after both steps finish, the row is updated with `FinishedUtc`, `Status = 'Succeeded'` and the three counts.
3. **Failure:** if a step throws, the row is updated with `FinishedUtc`, `Status = 'Failed'`, `FailureStage` and `FailureType`, and the exception is rethrown so the Functions host still marks the invocation failed.
4. **Abandoned runs:** if the process stops mid-run, the row stays `Running` with no `FinishedUtc`. Treat a `Running` row older than a set time (for example 30 minutes) as abandoned. Nothing is written to fix it.
5. **Logs stay.** The existing log line is kept. The table adds to the logs; it does not replace them.

### Ownership and rules

- **Writes:** FileScan only. **Reads:** web app, later, for a "last run" view. No web endpoint is included in this proposal.
- Follows `FileMonitoringWeb/CLAUDE.md`, "Shared database": the script goes in `sql/`, `sql/CONTRACT.md` gets a new section, and the FileScan change goes in the same commit or a commit pushed together. Both projects must build, and the production run must complete after deploy.
- Additive only. No existing table or column changes.

### Volume

At the example schedule (every 5 minutes) that is 288 rows a day, about 105,000 a year. This is small, so no archiving is needed in V1.

## Decisions needed

1. **Approve the table** as described, or change the columns.
2. **Manual runs:** the `ConnectivityTestHttp` check is not a production run, so it is not recorded. Confirm that.
3. **If the start insert fails** (SQL unreachable), should the run still proceed and log the failure? Recommended: yes. The scan should not stop because the log cannot be written.
4. **Retention:** keep all rows for now (recommended), or delete rows older than a set period later?
5. **Stale `Running` rows:** use the 30-minute rule above, or another time?

## Out of scope

- A web screen or API for scan runs.
- Manual scan triggers and the `Manual` trigger type (reserved only).
- Notifications when a run fails.
