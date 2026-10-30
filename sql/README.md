# Database scripts

Schema for the File Monitoring solution, shared by this web app and the FileScan Function App (`../FileScan`). This folder is the only place schema changes are made. Read the "Shared database" section in `../CLAUDE.md` before adding a script.

## Scripts

| Script | What it does |
| --- | --- |
| `001_create_monitoring_definition.sql` | Creates schema `FileScan` and table `FileScan.MonitoringDefinition`, and seeds the BNP test definition (Id 1). Moved here from the FileScan repo. |
| `003_match_type_and_days.sql` | Adds `MatchType` (Wildcard or Regex) and `DaysOfWeek` to `MonitoringDefinition`, and `MatchType` to `ExpectedDelivery`. Requires 002. |
| `004_seed_production_flows.sql` | Inserts the 62 production flows from the flow list, all **disabled**. Insert only: existing rows are never changed. Requires 003. |
| `002_create_v1_schema.sql` | Adds the V1 configuration columns to `MonitoringDefinition`, and creates `MonitoringDefinitionAudit`, `ExpectedDelivery`, `DeliveryStatusTransition`, `FileObservation`, `DeliveryWarning` and `IncidentComment`. Additive only. Requires 001. |
| `005_create_scan_run.sql` | Creates `FileScan.ScanRun`, one row per production scan run. The Function App writes it and the web app reads it for the "Last scan" line. |
| `006_create_data_health.sql` | Creates the Data Health tables `FileScan.DataHealthSource`, `FileScan.DataHealthControl` and `FileScan.DataHealthControlAudit`. |
| `006_seed_data_health_sources_TEMPLATE.sql` | Template for the approved Data Health sources. Copy it, fill in the rows, and run the copy once. It inserts nothing as it is. |
| `007_text_date_sources.sql` | Adds `DateColumnType` and `DateStyles` to `DataHealthSource`, so a text date column can be counted. Requires 006. |
| `008_data_health_source_format_audit.sql` | Adds `ConcurrencyToken` to `DataHealthSource` and creates `DataHealthSourceAudit`, so the web app can change a source's date format with an audit trail. Requires 007. |

## What to run now

Run these in order against the shared database. Skip any you have already run; `001` and `002` should already be applied.

1. `003_match_type_and_days.sql` (adds `MatchType` and `DaysOfWeek`)
2. `004_seed_production_flows.sql` (adds the 62 production flows, all disabled)

Deploy the web app and the FileScan Function App after `003`, before enabling any imported flow.

For Data Health with text date columns, run `007_text_date_sources.sql` and then `008_data_health_source_format_audit.sql`, after `006`. Then run your filled-in approved-source script, which sets `DateColumnType` and `DateStyles` for each source.

## Running a script

Run the scripts in number order against the database. Use the sqlcmd command below, or SSMS in SQLCMD mode, or paste the file into SSMS and run it there. The scripts are idempotent, so running them again is safe.

```bash
sqlcmd -b -S <server> -d <database> -G -i 001_create_monitoring_definition.sql
sqlcmd -b -S <server> -d <database> -G -i 002_create_v1_schema.sql
```

`-b` stops on the first error. `-G` uses Microsoft Entra ID sign-in; use `-U` and `-P` instead if the server uses a SQL login, and never put the password in a file that is committed.

After 002, expect the two result sets at the end of the script to show 7 tables in schema `FileScan` and 27 columns on `MonitoringDefinition`.

Permissions are not granted by these scripts. Section 9 of 002 has commented `GRANT` statements to fill in for the Web App and Function App users.

Scripts run in number order. Add new scripts as the next number; never rename or edit one that has already been applied.

## Before merging a change

This is a proof of concept with one live database. Follow the rules in `../CLAUDE.md`, "Shared database":

1. Update `CONTRACT.md` in the same change.
2. Commit the schema change and the code that uses it together.
3. Build both projects.
4. After deploying, run the two smoke checks.
