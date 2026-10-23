# Database contract

Which tables and columns each project uses in the shared database. Keep this up to date in the same change as any schema change (see "Shared database" in `../CLAUDE.md`).

Projects:

- **Web**: this repo, ASP.NET Core API (`Server/`).
- **FileScan**: `../FileScan`, Azure Function app (`FileScannerFunctionApp/`).

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

## Planned tables (not created yet)

From `../File_Monitoring_V1_Specifications.md`, sections 25–28. Add a row here when a script creates the table.

| Table | Written by |
| --- | --- |
| Configuration audit | Web |
| Comments / reasons | Web |
| Expected-delivery records | FileScan |
| Current delivery status | FileScan |
| Status-transition audit | FileScan |
| File evidence / observations | FileScan |
| Warnings | FileScan |

Both projects read from these tables as the V1 features are built.
