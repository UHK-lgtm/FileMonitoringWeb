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

This is a proof of concept with one live database. Follow the rules in `../CLAUDE.md`, "Shared database":

1. Update `CONTRACT.md` in the same change.
2. Commit the schema change and the code that uses it together.
3. Build both projects.
4. After deploying, run the two smoke checks.
