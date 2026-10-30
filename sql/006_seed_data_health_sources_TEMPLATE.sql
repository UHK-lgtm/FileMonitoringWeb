-- TEMPLATE: copy this file, fill in the approved sources, and run it once after 006_create_data_health.sql
-- (and after 007 and 008). Do not run the template as it is. Each row names a table or view that Data Health may count.
--
-- For each source:
--   SourceKey        a short stable key, unique (letters, digits and underscores)
--   DisplayName      the name shown on the page
--   SchemaName       schema of the table or view
--   ObjectName       table or view name. Spaces are allowed; the name is quoted when it is used.
--   DateColumn       the date (or datetime) column the counts are grouped by. Spaces are allowed. It must be indexed for the page to stay fast.
--   DateColumnType   date, datetime, datetime2, smalldatetime, datetimeoffset, varchar, nvarchar, char or nchar
--   DateStyles       text columns only: ordered convert styles, comma separated, e.g. 112 or 112,23,103. NULL for real date columns.
--
-- Insert only: a source whose SourceKey already exists is left unchanged. To change the format of an existing
-- source, use the Configure tab of the web app (or an UPDATE by an administrator).

SET NOCOUNT ON;
GO

MERGE FileScan.DataHealthSource AS target
-- The WHERE 1 = 0 keeps this template from inserting anything. Remove it, and add one row per source
-- in the VALUES list (uncomment the example line and replace it), then run the script.
USING (
    SELECT * FROM (VALUES
        -- (N'example_positions', N'Example positions', N'dbo', N'ExamplePositions', N'PositionDate', N'date', NULL),
        -- (N'example_feed', N'Example feed', N'FileScan', N'Example Feed', N'Trade Date', N'varchar', N'112,23')
        (CAST(NULL AS nvarchar(60)), CAST(NULL AS nvarchar(200)), CAST(NULL AS nvarchar(128)), CAST(NULL AS nvarchar(128)),
         CAST(NULL AS nvarchar(128)), CAST(NULL AS nvarchar(20)), CAST(NULL AS nvarchar(100)))
    ) AS v (SourceKey, DisplayName, SchemaName, ObjectName, DateColumn, DateColumnType, DateStyles)
    WHERE 1 = 0
) AS source (SourceKey, DisplayName, SchemaName, ObjectName, DateColumn, DateColumnType, DateStyles)
    ON target.SourceKey = source.SourceKey
WHEN NOT MATCHED BY TARGET THEN
    INSERT (SourceKey, DisplayName, SchemaName, ObjectName, DateColumn, DateColumnType, DateStyles, IsActive)
    VALUES (source.SourceKey, source.DisplayName, source.SchemaName, source.ObjectName, source.DateColumn,
            source.DateColumnType, source.DateStyles, 1);
GO
