# API contract (V1 web)

The contract between the ASP.NET Core API (`Server/`) and the React app (`ClientApp/`). Both sides build to this document. Change it here first, then in the code.

Scope: V1 as in `docs/File_Monitoring_V1_Specifications.md`, sections 19–23 and 25–27. Roles (Viewer/Admin) are not in this version. See `docs/ISSUES.md`.

## Conventions

- Base path: `/api`. Same origin as the React app, so no CORS.
- All endpoints require Microsoft Entra ID sign-in. App Service enforces it. A request without a session gets 401 or a redirect, and the API returns no data.
- The signed-in user comes from the `X-MS-CLIENT-PRINCIPAL-NAME` header that App Service adds. It is used for `ChangedBy` and `CreatedBy`. A request without it is rejected with 401.
- JSON uses camelCase. Dates are ISO 8601. `…Utc` fields are UTC. `date` values are `yyyy-MM-dd`. `time` values are `HH:mm`.
- Errors: `{ "errors": ["message", ...] }` for validation (400). `{ "message": "..." }` for other errors. Never SQL text, connection strings or stack traces.
- Status values are exactly `Pending`, `OnTime`, `Missing`, `Incomplete`, `Late`. The UI shows "On Time" for `OnTime`.

## Deliveries (read-only, plus comments)

### `GET /api/deliveries?date=yyyy-MM-dd`

Expected deliveries for one local date (Europe/Luxembourg). `date` is optional and defaults to today in Europe/Luxembourg.

```json
{
  "date": "2026-10-23",
  "items": [
    {
      "expectedDeliveryId": 1,
      "monitoringDefinitionId": 1,
      "definitionName": "BNP Fund Accounting Test",
      "expectedDate": "2026-10-23",
      "status": "Pending",
      "expectedFileCount": 1,
      "receivedFileCount": 0,
      "unexpectedFileCount": 0,
      "deadlineUtc": "2026-10-23T07:00:00Z",
      "completedAtUtc": null,
      "warningCount": 0
    }
  ]
}
```

### `GET /api/deliveries/{id}`

Detail for one expected delivery.

```json
{
  "delivery": {
    "expectedDeliveryId": 1,
    "monitoringDefinitionId": 1,
    "definitionName": "BNP Fund Accounting Test",
    "expectedDate": "2026-10-23",
    "relativePath": "BNP_FUND_ACCOUNTING/IN/PRD/test",
    "fileNamePattern": "LU_PO_FIO_20260112.xlsx",
    "timeZoneId": "Europe/Luxembourg",
    "status": "Pending",
    "expectedFileCount": 1,
    "receivedFileCount": 0,
    "unexpectedFileCount": 0,
    "deadlineUtc": "2026-10-23T07:00:00Z",
    "completedAtUtc": null
  },
  "transitions": [
    { "fromStatus": null, "toStatus": "Pending", "occurredAtUtc": "...", "recordedAtUtc": "...", "expectedFileCount": 1, "receivedFileCount": 0, "deadlineUtc": "..." }
  ],
  "files": [
    { "fileName": "...", "relativePath": "...", "sizeBytes": 6134328, "fileCreatedUtc": "...", "fileLastModifiedUtc": "...", "observedAtUtc": "...", "isUnexpected": false }
  ],
  "warnings": [
    { "warningType": "UnexpectedFileWarning", "fileName": "", "detail": "1 file(s) beyond the expected count.", "detectedAtUtc": "...", "resolvedAtUtc": null }
  ],
  "comments": [
    { "commentId": 1, "commentText": "...", "createdBy": "user@...", "createdAtUtc": "..." }
  ]
}
```

`transitions` are ordered by `occurredAtUtc`. `comments` are ordered by `createdAtUtc`. Unknown ids return 404.

### `POST /api/deliveries/{id}/comments`

Body: `{ "text": "..." }`. `text` is required, trimmed, and at most 2000 characters. Returns 201 with one comment object as in `comments`. Comments are append-only.

## Monitoring definitions (configuration)

### Definition object

All fields are returned by `GET`. `POST` and `PUT` accept the same fields, except `id`, `isRetired` and `concurrencyToken` which the server sets.

```json
{
  "id": 1,
  "name": "BNP Fund Accounting Test",
  "provider": "BNP",
  "relativePath": "BNP_FUND_ACCOUNTING/IN/PRD/test",
  "fileNamePattern": "LU_PO_FIO_20260112.xlsx",
  "matchType": "Wildcard",
  "expectedFileCount": 1,
  "frequency": "Daily",
  "daysOfWeek": null,
  "isoWeekday": null,
  "intervalDays": null,
  "anchorDate": null,
  "dayOfMonth": null,
  "monthOfQuarter": null,
  "expectedDeliveryTime": "09:00",
  "bufferMinutes": 0,
  "timeZoneId": "Europe/Luxembourg",
  "filenameDateMode": "NoDate",
  "filenameDateFormat": null,
  "sizeCheckEnabled": false,
  "referenceSizeBytes": null,
  "sizeTolerancePercent": null,
  "isEnabled": true,
  "isRetired": false,
  "concurrencyToken": "AAAAAAAAB9E="
}
```

Field rules (the server checks these, and the SQL check constraints back them up):

- `name`: required, at most 200 characters.
- `provider`: at most 100 characters. May be empty.
- `relativePath`: relative to `IN/PRD`'s share root. No leading or trailing `/`, no `\`, no `:`, no empty, `.` or `..` segments. Empty means the share root.
- `matchType`: `Wildcard` or `Regex`. Required. `Wildcard` is the default for new definitions.
- `fileNamePattern`: required, at most 260 characters, no `/`. For `Wildcard`, no `\`, and `*` and `?` are wildcards, matched case-insensitively. For `Regex`, a regular expression that must match the whole file name (case-insensitive). Backslashes are allowed. It must compile.
- `daysOfWeek`: null, or a list of ISO weekdays with no duplicates, each 1 (Monday) to 7 (Sunday). Null or an empty list means every day. A delivery is planned only on these days. Stored as a comma list such as `1,2,3,4,5`.
- `expectedFileCount`: at least 1.
- `frequency`: one of `Daily`, `Weekly`, `EveryNDays`, `Monthly`, `Quarterly`.
  - `Weekly`: `isoWeekday` 1 (Monday) to 7 (Sunday) is required.
  - `EveryNDays`: `intervalDays` at least 1, and `anchorDate` is required.
  - `Monthly`: `dayOfMonth` 1 to 31 is required. 31 means the last day in shorter months.
  - `Quarterly`: `dayOfMonth` 1 to 31 and `monthOfQuarter` 1 to 3 are required.
  - Fields that do not apply to the frequency must be null.
- `expectedDeliveryTime`: `HH:mm`.
- `bufferMinutes`: 0 to 1440.
- `timeZoneId`: a valid IANA time zone, required.
- `filenameDateMode`: one of `NoDate`, `SameDay`, `PreviousDay`, `MonthYear`. The `fileNamePattern` must contain `{date}` when the mode is not `NoDate`, and must not contain it when the mode is `NoDate`.
- `filenameDateFormat`: required when the mode is not `NoDate`. A .NET date format such as `yyyyMMdd`.
- `sizeCheckEnabled`: when true, `referenceSizeBytes` is above 0 and `sizeTolerancePercent` is 0 or more.

### `GET /api/monitoring-definitions`

Array of definition objects, including retired ones (`isRetired: true`). Ordered by `id`.

### `GET /api/monitoring-definitions/{id}`

One definition object. 404 if unknown.

### `POST /api/monitoring-definitions`

Creates a definition. Returns 201 with the new object. Writes one audit row with `fieldName` `(created)` and `newValue` set to the name.

### `PUT /api/monitoring-definitions/{id}`

Body is the full definition, including `concurrencyToken` as last read. Returns 200 with the updated object. Writes one audit row per changed field, with the old and new values. Returns 409 if `concurrencyToken` no longer matches, meaning someone else changed the row. The UI then reloads the definition.

### `POST /api/monitoring-definitions/{id}/enabled`

Body: `{ "isEnabled": true }`. Enables or disables the definition. Writes an audit row for `isEnabled`. A retired definition cannot be enabled (400).

### `POST /api/monitoring-definitions/{id}/retire`

Soft delete. Sets `isRetired` true, `isEnabled` false, and records who and when. Writes an audit row for `isRetired`. Hard delete is not offered. Returns 200 with the object.

### `GET /api/monitoring-definitions/{id}/audit`

Configuration audit for one definition, newest first:

```json
[
  { "auditId": 1, "changedAtUtc": "...", "changedBy": "user@...", "fieldName": "fileNamePattern", "oldValue": "...", "newValue": "..." }
]
```

## Export

### `GET /api/export/audit.xlsx`

Excel workbook built from saved SQL data only. It does not scan the file share. Sheets:

- `Configuration audit`: every audit row, with the definition id and name.
- `Deliveries`: every expected delivery, with status, counts, deadline, completion time and warning count.

Served as `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` with a file name of `file-monitoring-audit-{yyyyMMdd-HHmm}.xlsx`.

## Scan status (read-only)

### `GET /api/scan-runs/latest`

The latest production scan run, and what it means for the "Last scan" line on Today.

```json
{
  "state": "Current",
  "latest": {
    "scanRunId": 42,
    "startedUtc": "2026-10-27T08:05:00Z",
    "finishedUtc": "2026-10-27T08:05:03Z",
    "status": "Succeeded",
    "scheduledToday": 3,
    "openDeliveries": 5,
    "statusChanges": 1
  }
}
```

`state` is one of:

- `NoRunRecorded`: no run has been recorded (`latest` is null).
- `Current`: the latest run succeeded within the last 15 minutes.
- `Stale`: the latest run succeeded more than 15 minutes ago.
- `Running`: a run is in progress.
- `DidNotFinish`: a run has been `Running` for more than 30 minutes.
- `Failed`: the latest run failed.
- `Unknown`: the status is not recognised, or the start time is in the future.

The failure stage and failure type are not returned. They are in the logs, for operators. If the run table cannot be read (for example before `sql/005` has been run), the response is `503` with the title `Scan status is unavailable.`

## Data Health

Record-count health checks against approved SQL sources (`docs/Data_Health_Basic_Specifications.md`). Reads run the approved checks. Writes change only the control configuration, and every change is audited. Statuses are `Missing`, `Decrease`, `Increase`, `Stable`, `NoHistory`, and `Error` (a check that could not run; never stored).

- `GET /api/data-health`: the page. Returns `today`, `refreshedAtUtc`, the summary (`enabledSources`, `missingCount`, `decreaseCount`, `errorCount`, `healthy`) and one result per enabled control in display order. Each result has `controlDate`, `controlCount`, `comparisonDate`, `previousCount`, `percentChange`, `latestVisibleDate`, `latestVisibleCount`, `status`, `attention`, `message`, `trend` (one point per day in the window, zeros included) and `unreadableCount`. `unreadableCount` is the number of rows in the whole source whose text date matches none of the configured styles. Blank text counts as unreadable; a NULL does not. Cached for 30 seconds.
- `GET /api/data-health/sources`: the approved sources the web app may count. Each has `id`, `sourceKey`, `displayName`, `schemaName`, `objectName`, `dateColumn`, `dateColumnType`, `dateStyles` (comma-separated, text columns only, otherwise `null`) and `concurrencyToken`. Only an admin adds sources (see `sql/006_seed_data_health_sources_TEMPLATE.sql`).
- `PUT /api/data-health/sources/{id}/format`: body `{ dateColumnType, dateStyles, concurrencyToken }`. Changes only the date type and styles of an existing source; the schema, table, view and column names cannot be changed here. Returns 200 with the source. 400 with `errors` for an unknown type, a text type without styles, styles on a date type, or a bad style list. 409 when the source changed since it was read. Every change is audited.
- `GET /api/data-health/sources/{id}/audit`: the format change history, newest first.
- `GET /api/data-health/controls`, `GET /api/data-health/controls/{id}`: the configuration.
- `POST /api/data-health/controls`: body `{ name, displayOrder, isEnabled, approvedSourceId, offsetDays, offsetType, historyDays, missingCheck, decreaseCheck, decreaseTolerancePercent }`. Returns 201 with the control. 400 with `errors` for a bad field or an unapproved source.
- `PUT /api/data-health/controls/{id}`: the same body plus `concurrencyToken`. 409 when the control changed since it was read.
- `POST /api/data-health/controls/{id}/enabled` (`{ isEnabled }`), `POST /api/data-health/controls/{id}/retire`: a retired control cannot be enabled.
- `GET /api/data-health/controls/{id}/audit`: the audit trail, newest first.

Ranges: `offsetDays` 0-60, `historyDays` 1-365, `decreaseTolerancePercent` 0-100, `displayOrder` 0 or more. `offsetType` is `CalendarDays` or `Weekdays`.

Date formats: `dateColumnType` is one of `date`, `datetime`, `datetime2`, `smalldatetime`, `datetimeoffset`, `varchar`, `nvarchar`, `char`, `nchar`. A text type needs `dateStyles`: one or more SQL Server convert styles from the allowed list, comma-separated, in the order they are tried (for example `112,23`). Each style may appear once. A date type must have no styles.

## Not in this version

- Roles (Viewer and Admin). The spec defers them until configuration editing exists. Every signed-in user can edit.
- "Not Expected" in the deliveries list. The API does not evaluate schedules for the UI yet.
- Excel export of comments and file observations beyond the two sheets above.
