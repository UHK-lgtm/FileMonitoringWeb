# Database scripts

Schema for the File Monitoring solution, shared by this web app and the FileScan Function App (`../FileScan`). This folder is the only place schema changes are made. Read the "Shared database" section in `../CLAUDE.md` before adding a script.

## Scripts

| Script | What it does |
| --- | --- |
| `001_create_monitoring_definition.sql` | Creates schema `FileScan` and table `FileScan.MonitoringDefinition`, and seeds the BNP test definition (Id 1). Moved here from the FileScan repo. |
| `002_create_v1_schema.sql` | Adds the V1 configuration columns to `MonitoringDefinition`, and creates `MonitoringDefinitionAudit`, `ExpectedDelivery`, `DeliveryStatusTransition`, `FileObservation`, `DeliveryWarning` and `IncidentComment`. Additive only. Requires 001. |

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
