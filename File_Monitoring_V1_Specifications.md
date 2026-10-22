# File Monitoring V1 Specifications

## 1. Purpose

This document is the implementation handover for the File Monitoring V1 solution. It consolidates the agreed business rules and separates responsibilities between:

- the FileScanner Azure Function project;
- the ASP.NET Core Web API;
- the React frontend; and
- SQL Server as the shared persistence and integration boundary.

The Web App must not directly access, move, rename, delete, or otherwise modify files or folders in Azure File Share. The Web App changes monitoring configuration in SQL. FileScanner reads the current configuration and performs read-only scanning.

---

## 2. V1 Architecture

```text
React Frontend
      |
      v
ASP.NET Core API
      |
      v
SQL Server
   ^       |
   |       | monitoring configuration
   |       | configuration audit
   |       | comments and reporting data
   |
FileScanner Function
      |
      v
Azure File Share: generalisa
```

SQL Server is the integration boundary. React never connects directly to SQL, and the Web App never scans Azure Files.

---

## 3. Monitoring Scope

V1 monitors inbound production delivery locations configured under:

```text
<Provider>/IN/PRD
```

The following archive styles are not monitored as V1 delivery sources:

```text
IN/ARCHIVE/PRD
IN/ARCHIVE_PRD
```

Paths are stored relative to the `generalisa` Azure File Share.

---

## 4. Independent Monitoring Definitions

Each logical feed or file family is represented by its own Monitoring Definition.

Different files with different paths, filename patterns, schedules, deadlines, expected counts, or quality rules are separate Monitoring Definitions and are evaluated independently.

Example:

```text
Definition A
Path A
Pattern A
Schedule A
Deadline A
Independent status A

Definition B
Path B
Pattern B
Schedule B
Deadline B
Independent status B
```

The status of one Monitoring Definition must never change the status of another Monitoring Definition.

### Expected file count scope

`ExpectedFileCount` applies only inside one Monitoring Definition.

```text
ExpectedFileCount = 5
```

means that one definition expects five physical files matching that definition's path, pattern, date rule, and expected period.

It does not mean that five unrelated Monitoring Definitions are grouped into one delivery.

---

## 5. Monitoring Definition Configuration

Each Monitoring Definition must support the following concepts:

### Identity

- Name
- Provider
- Enabled/disabled state

### Location and matching

- Relative `IN/PRD` path
- Filename pattern
- Expected file count

### Schedule

- Frequency
- Frequency-specific schedule values
- Expected delivery time
- Buffer/tolerance
- Timezone

### Filename date handling

- Filename date mode
- Filename date format where applicable

### Optional quality checks

- Size check enabled/disabled
- Reference/expected size
- Size tolerance

The Web App may change these configuration values in SQL. It must not modify the actual File Share contents.

---

## 6. Schedule Types

V1 supports:

- Daily
- Weekly
- Every N days, including every 15 days
- Monthly
- Quarterly

The scanner timer frequency is independent from individual delivery schedules.

---

## 7. Expected Delivery Creation

An expected-delivery record is created by the first FileScanner run that covers the applicable expected period.

For example, when the first scanner execution evaluates a definition whose schedule says a delivery is expected for the current day/period, FileScanner creates or ensures the existence of the logical expected-delivery record.

Repeated scanner executions must not create duplicates for the same Monitoring Definition and expected period.

If a definition has no expected delivery for the current period, FileScanner must not create a missing/incomplete delivery failure for that period.

---

## 8. Not Expected

If an enabled Monitoring Definition is not scheduled for the current day/period, the Web App may display:

```text
Not Expected
```

`Not Expected` is a neutral schedule state. It is not a delivery status and is not a failure.

It must not be stored or processed as `Pending`, `Missing`, `Incomplete`, `Late`, or `On Time`.

---

## 9. Delivery Time and Buffer

Each Monitoring Definition can have:

```text
Expected Delivery Time
Buffer / Tolerance
Timezone
```

The effective deadline is:

```text
Expected Delivery Time + Buffer
```

Example:

```text
Expected time: 09:00
Buffer:        30 minutes
Deadline:      09:30
```

A complete delivery received at or before 09:30 is `On Time`.

Arrival after the nominal expected time but inside the buffer is still `On Time`.

---

## 10. Delivery Status Model

V1 delivery statuses are:

- Pending
- On Time
- Missing
- Incomplete
- Late

### Pending

A delivery is expected for the current period, the delivery is not complete, and the effective deadline has not passed.

For a five-file definition:

```text
0/5 before deadline -> Pending
3/5 before deadline -> Pending
```

### On Time

The complete expected delivery is received on or before the effective deadline.

```text
5/5 by deadline -> On Time
```

### Missing

The effective deadline has passed and zero expected matching files were received.

```text
0/5 at deadline -> Missing
```

### Incomplete

The effective deadline has passed and more than zero but fewer than the expected number were received.

```text
3/5 at deadline -> Incomplete
```

### Late

The complete expected delivery is received only after the effective deadline.

Confirmed state evolution:

```text
Expected: 5 files
Deadline: 09:30

09:30
Received: 3/5
Current status: Incomplete
Audit event: 09:30 -> Incomplete

10:15
Received: 5/5
Current status: Late
Audit event: 10:15 -> Late
```

The earlier `Incomplete` or `Missing` deadline event must never be erased when the latest current status becomes `Late`.

---

## 11. Unexpected Additional Files

When the number of matching files exceeds the expected count, delivery timing remains evaluated normally, and an independent warning is added.

Example:

```text
Expected: 5
Received by deadline: 6
Delivery status: On Time
Warning: Unexpected File Warning
```

If the complete expected count was achieved only after the deadline, the delivery status is `Late`, while the independent warning remains `Unexpected File Warning`.

Warnings do not overwrite delivery status.

The physical unexpected file(s) must be retained as file evidence for audit.

---

## 12. Filename Date Rules

V1 supports:

- No Date
- Same Day
- Previous Day
- Month + Year
- Configurable date format

Example formats:

```text
yyyyMMdd
yyyy-MM-dd
ddMMyyyy
yyyyMM
MMyyyy
```

Delivery frequency and filename date convention are independent configuration dimensions.

Calendar-aware Previous Business Day is outside V1 until regional business calendars are introduced.

---

## 13. Public Holidays

V1 does not apply automatic public-holiday exceptions.

Do not hard-code one global Luxembourg or other regional holiday calendar.

Future versions may associate Monitoring Definitions with regional business calendars.

---

## 14. File Metadata and Evidence

For every matched physical file, retain available evidence including:

- Monitoring Definition identifier
- Provider
- Actual filename
- Relative/source path
- File size
- File-created timestamp where available
- File last-modified timestamp where available
- Scanner detected/observed timestamp
- Expected delivery/reporting period

Delivery-level evidence must retain:

- Expected period/date
- Expected delivery time
- Buffer
- Effective deadline
- Expected file count
- Received file count
- Current delivery status
- Significant status transitions and timestamps
- Warnings

---

## 15. Updated or Replaced File Audit

If a previously observed filename is updated, replaced, or re-uploaded, FileScanner must preserve the observation for audit rather than silently overwriting all earlier evidence.

The system must record enough information to show that the file was observed again with changed metadata, such as:

- detection timestamp;
- file size;
- created timestamp where available;
- last-modified timestamp where available; and
- relationship to the same logical expected delivery.

The latest file evidence may be used for the current view, but earlier observations must remain available in audit history.

A re-upload or metadata change must not create duplicate identical observations on every scanner run. A new audit observation is required when meaningful file metadata changes.

---

## 16. Warning Model

Warnings are independent from delivery status.

V1 warning types include:

- Size Warning
- Unexpected File Warning

Valid examples:

```text
Delivery status: On Time
Warning: Size Warning
```

```text
Delivery status: On Time
Warning: Unexpected File Warning
```

```text
Delivery status: Late
Warnings:
- Size Warning
- Unexpected File Warning
```

Warnings must never replace the delivery status.

---

## 17. Size Warning

Size checking is optional per Monitoring Definition.

Configuration concepts:

```text
Enable Size Check
Reference / Expected Size
Tolerance
```

A size anomaly creates a `Size Warning`. V1 uses configured reference/tolerance rules rather than statistical or machine-learning anomaly detection.

---

## 18. Current State vs Audit History

The system stores both:

1. current/latest delivery state; and
2. significant historical transitions and evidence.

Examples:

```text
09:30 -> Incomplete
10:15 -> Late
```

or:

```text
09:30 -> Missing
12:00 -> Late
```

Latest state is used for current monitoring. Earlier states remain for audit/provider escalation.

---

## 19. Configuration Effective Timing

Configuration changes made during the day become effective on the same day.

The API must audit the change, including authenticated user, timestamp, changed field, old value, and new value.

Historical delivery events already evaluated before the change must preserve the effective values and status evidence that applied at their earlier evaluation times.

After the change is saved, subsequent scanner runs use the new current configuration.

The implementation must not retroactively rewrite existing audit events merely because the current configuration changed.

---

## 20. Configuration Change Audit

Every configuration change made through the Web/API must be auditable.

Capture at least:

- MonitoringDefinitionId
- ChangedAtUtc
- Authenticated ChangedBy identity
- Changed field
- Previous value
- New value

Auditable fields include:

- Relative path
- Filename pattern
- Expected file count
- Frequency/schedule
- Expected delivery time
- Buffer/tolerance
- Timezone
- Filename date mode
- Filename date format
- Size-check settings
- Enabled/disabled/retired state

Configuration audit is separate from delivery audit.

---

## 21. Soft Delete / Retirement

Monitoring Definitions must not be hard-deleted in V1.

Deleting from the user interface performs a soft delete or retirement, preserving:

- configuration history;
- delivery history;
- file evidence;
- warnings;
- comments; and
- provider-escalation audit data.

A retired definition must not generate new expected deliveries after its effective retirement time.

Suggested domain concept:

```text
IsDeleted / IsRetired
DeletedAtUtc / RetiredAtUtc
DeletedBy / RetiredBy
```

The exact SQL field names may be chosen during database design, but hard deletion is not allowed for a definition with audit history.

---

## 22. Comments and Reasons

Authenticated users may append comments/reasons to monitoring incidents.

Each comment must retain:

- author identity;
- timestamp; and
- comment/reason text.

Automated FileScanner processing must not overwrite human comments.

---

## 23. Excel Audit Export

The ASP.NET Core API must provide Excel export based on persisted SQL audit data.

The export is intended for audit, investigation, and provider escalation and should include relevant configuration, expected timing, counts, statuses, warnings, file evidence, file timestamps, status transitions, and comments.

The export must not directly rescan Azure Files.

---

## 24. V1 Notifications

V1 does not integrate:

- Microsoft Teams notifications;
- automated email notifications; or
- iTop ticket creation/update.

These are future features built on persisted monitoring outcomes.

---

## 25. Function App Responsibilities

FileScanner owns:

- loading current enabled/non-retired configurations from SQL;
- determining which definitions are expected in the current period;
- creating/ensuring expected-delivery records on the first covering scan;
- scanning configured `IN/PRD` paths;
- matching relevant files;
- capturing file evidence;
- detecting changed/re-uploaded file evidence;
- computing expected/received counts;
- evaluating effective deadlines;
- calculating Pending, On Time, Missing, Incomplete, and Late;
- calculating Size Warning and Unexpected File Warning;
- persisting current state and automated audit transitions;
- ensuring idempotency and avoiding duplicate records.

FileScanner does not own user-driven configuration audit, comments, Excel generation, or Web authentication.

---

## 26. ASP.NET Core API Responsibilities

The API owns:

- validation and persistence of Monitoring Definition changes;
- configuration CRUD using soft-delete/retirement rather than hard delete;
- same-day configuration changes;
- configuration audit entries;
- authenticated ChangedBy identity;
- current monitoring queries;
- delivery-history queries;
- configuration-audit queries;
- comment/reason persistence;
- Excel export generation;
- server-side authorization and validation.

React never writes SQL directly.

---

## 27. React Frontend Responsibilities

React owns the user experience for:

- viewing today's monitoring;
- showing independent statuses per Monitoring Definition;
- showing `Not Expected` as a neutral schedule indicator;
- creating/editing/enabling/disabling/retiring definitions;
- editing path configuration without modifying File Share contents;
- schedule, deadline, buffer, date-mode, count, and size-check forms;
- displaying expected/received counts;
- displaying warnings separately from delivery status;
- viewing delivery history;
- viewing configuration audit;
- adding comments/reasons; and
- requesting Excel exports from the API.

---

## 28. SQL Persistence Boundary

SQL Server is the shared integration and persistence layer for:

- Monitoring Definitions;
- schedule and matching configuration;
- configuration audit;
- expected-delivery records;
- current delivery status;
- status-transition audit;
- physical file evidence/observations;
- warnings;
- comments/reasons; and
- data used in Excel exports.

---

## 29. Idempotency

Repeated scanner executions must not create duplicate:

- expected-delivery records;
- identical physical-file observations;
- status transitions; or
- warnings that represent the same unchanged condition.

The exact SQL keys/upsert strategy will be finalized during database design.

---

## 30. Future Features

Outside V1:

- regional business/holiday calendars;
- calendar-aware Previous Business Day;
- Microsoft Teams notifications;
- email notifications;
- iTop ticketing integration;
- advanced/statistical anomaly detection; and
- other warning types not defined in this document.
