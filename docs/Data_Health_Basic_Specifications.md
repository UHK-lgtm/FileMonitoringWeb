# Data Health - Basic Specifications

## 1. Purpose

The Data Health feature provides a configurable Web App page for checking the health of SQL data sources on demand.

The feature is separate from FileScanner file-delivery processing. It runs through the React frontend and ASP.NET Core API against the existing SQL Server connection.

```text
React Data Health page
        |
        v
ASP.NET Core API
        |
        v
SQL Server
        |
        v
Current health results returned on refresh
```

## 2. V1 Scope

V1 supports SQL record-count health controls similar to the existing Daily Data Health Report.

The page must show:

- Control name / data source
- Control date
- Record count for the control date
- Previous comparison date
- Previous record count
- Percentage change
- Latest visible date
- Latest visible count
- Status
- Historical record-count trend

## 3. Control Statuses

V1 statuses are:

- `MISSING`: no data exists for the configured control date.
- `DECREASE`: the control-date count is lower than the configured comparison count/rule.
- `INCREASE`: the control-date count is higher than the comparison count.
- `STABLE`: the control-date count equals the comparison count.
- `NO_HISTORY`: no earlier comparison data is available.

`MISSING` and `DECREASE` are attention conditions. `INCREASE`, `STABLE`, and `NO_HISTORY` are displayed for information unless later configuration defines different behavior.

## 4. Refresh Behavior

Data Health is evaluated on demand.

When the user opens or refreshes the Data Health page:

1. React calls the ASP.NET Core API.
2. The API loads enabled Data Health control definitions.
3. The API executes the approved SQL health checks.
4. The API calculates the current statuses and trend information.
5. The API returns the result to React.

V1 does not require the Azure Function or background scheduling for this feature.

## 5. Configurable Control Definition

A Data Health control should be configurable from the Web App with fields such as:

- Control name
- Display order
- Enabled/disabled state
- Approved source identifier
- Source schema
- Source table or view
- Date column
- Control-date offset
- Offset type, initially calendar days or weekdays
- History window in days
- Comparison rule, initially previous available date
- Missing-data check enabled
- Record-count decrease check enabled
- Optional decrease tolerance/threshold

The exact database field names can be finalized during SQL schema design.

## 6. Query Safety

The UI must not allow unrestricted arbitrary SQL statements.

The API must execute only approved, server-side query patterns or approved stored procedures. User-configurable values must be validated and used through safe SQL handling.

The React application must never connect directly to SQL Server.

## 7. Control Date

The existing report uses a T-2 weekday control date and shows ten calendar days before the control date, while dates after the control date are informational only.

V1 should make the control-date offset and history window configurable per control or through an approved shared setting.

Public-holiday calendars are outside the initial scope unless separately specified.

## 8. Data Health Page

The React page should include:

### Summary

- Control date
- Number of enabled sources
- Missing count
- Decrease count
- Overall healthy/attention indicator

### Status table

- Data source
- Control date
- Control count
- Comparison date
- Previous count
- Percentage change
- Latest visible date
- Latest visible count
- Status

### Trend view

A simple historical trend for each configured source using the selected history window.

## 9. Configuration Page

Authorized users should be able to:

- Add a Data Health control
- Edit a Data Health control
- Enable or disable a control
- Change display order
- Configure control-date and comparison settings
- Soft-delete or retire a control

Configuration changes should be auditable with:

- Changed by
- Changed at
- Changed field
- Previous value
- New value

## 10. Responsibilities

### React

- Display Data Health summary, status table, and trends
- Provide refresh action
- Provide configuration forms
- Show validation and query errors safely

### ASP.NET Core API

- Read and validate Data Health configuration
- Execute approved SQL health checks
- Calculate status and percentage change
- Return current results to React
- Persist configuration and configuration audit
- Prevent arbitrary SQL execution

### FileScanner Function

- No responsibility for Data Health V1
- Continues to handle Azure File Share delivery monitoring only

## 11. V1 Out of Scope

- Azure Function execution for Data Health
- Scheduled email reports
- Microsoft Teams notifications
- iTop ticket creation
- Arbitrary SQL entered by users
- Automated historical snapshots on every refresh
- Regional public-holiday calendars

## 12. Definition of Done

V1 is complete when an authorized user can configure approved SQL-based health controls, open the Data Health page, refresh it, and see current counts, comparison values, percentage changes, statuses, and trends returned through the ASP.NET Core API.
