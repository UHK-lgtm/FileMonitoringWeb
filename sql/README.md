# Database scripts

Schema for the File Monitoring solution, shared by this web app and the FileScan Function App (`../FileScan`). This folder is the only place schema changes are made. Read the "Shared database" section in `../CLAUDE.md` before adding a script.

## Scripts

| Script | What it does |
| --- | --- |
| `001_create_monitoring_definition.sql` | Creates schema `FileScan` and table `FileScan.MonitoringDefinition`, and seeds the BNP test definition (Id 1). Moved here from the FileScan repo. |

## Running a script

Run each script once against the database with SSMS or `sqlcmd`, in the whole-file session. Scripts are idempotent, so running them again is safe.

Scripts run in number order. Add new scripts as the next number; never rename or edit one that has already been applied.

## Before merging a change

Apply the script to a dev copy of the database, then check both projects against it (see `../CLAUDE.md`, "Shared database").
