-- TEMPLATE: copy this file, fill in the approved sources, and run it once after 006_create_data_health.sql
-- (and after 007, 008 and 009). Do not run the template as it is. Each row names a table or view that Data Health may count.
--
-- For each source:
--   SourceKey    a short stable key, unique (letters, digits and underscores)
--   DisplayName  the name shown on the page
--   SchemaName   schema of the table or view
--   ObjectName   table or view name. Spaces are allowed; the name is quoted when it is used.
--   DateColumn   the date (or datetime, or text) column the counts are grouped by. Spaces are allowed.
--                It should be indexed, or the page is slow on large tables.
--
-- The date format (real date type or text with convert styles) is not set here. It belongs to each control, and is set
-- in the control form on the Configure tab of the web app.
--
-- Insert only: a source whose SourceKey already exists is left unchanged.

SET NOCOUNT ON;
GO

MERGE FileScan.DataHealthSource AS target
-- The WHERE 1 = 0 keeps this template from inserting anything. Remove it, and add one row per source
-- in the VALUES list (uncomment the example line and replace it), then run the script.
USING (
    SELECT * FROM (VALUES
        -- (N'example_positions', N'Example positions', N'dbo', N'ExamplePositions', N'PositionDate'),
        -- (N'example_feed', N'Example feed', N'FileScan', N'Example Feed', N'Trade Date')
        (CAST(NULL AS nvarchar(60)), CAST(NULL AS nvarchar(200)), CAST(NULL AS nvarchar(128)), CAST(NULL AS nvarchar(128)),
         CAST(NULL AS nvarchar(128)))
    ) AS v (SourceKey, DisplayName, SchemaName, ObjectName, DateColumn)
    WHERE 1 = 0
) AS source (SourceKey, DisplayName, SchemaName, ObjectName, DateColumn)
    ON target.SourceKey = source.SourceKey
WHEN NOT MATCHED BY TARGET THEN
    INSERT (SourceKey, DisplayName, SchemaName, ObjectName, DateColumn, IsActive)
    VALUES (source.SourceKey, source.DisplayName, source.SchemaName, source.ObjectName, source.DateColumn, 1);
GO
