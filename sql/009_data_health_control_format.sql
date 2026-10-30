-- 009: the date format moves from the approved source to each Data Health control. Requires 007 and 008.
-- Additive and idempotent. A control can now read the same source differently from another control.
--
-- FileScan.DataHealthControl.DateColumnType and DateStyles are added, and every existing control is given the
-- format its source has today. The source's own DateColumnType and DateStyles are left in place for now; the web app
-- no longer reads them. They can be dropped in a later script once this has been checked on the database.
--
-- DateColumnType: date, datetime, datetime2, smalldatetime, datetimeoffset, varchar, nvarchar, char or nchar.
-- DateStyles:     text columns only. Ordered convert styles, comma separated, e.g. 112 or 112,23. NULL for date columns.
--
-- Run with:  sqlcmd -b -S <server> -d <database> -G -i 009_data_health_control_format.sql

SET NOCOUNT ON;
GO

-- Add the columns and copy the source formats into the existing controls, once. The copy is dynamic SQL because the
-- new column does not exist when this batch is compiled.
IF COL_LENGTH(N'FileScan.DataHealthControl', N'DateColumnType') IS NULL
BEGIN
    ALTER TABLE FileScan.DataHealthControl ADD DateColumnType nvarchar(20) NOT NULL
        CONSTRAINT DF_DataHealth_Control_DateColumnType DEFAULT (N'date');
    ALTER TABLE FileScan.DataHealthControl ADD DateStyles nvarchar(100) NULL;

    EXEC sys.sp_executesql N'
        UPDATE c
        SET c.DateColumnType = s.DateColumnType,
            c.DateStyles     = s.DateStyles
        FROM FileScan.DataHealthControl AS c
        JOIN FileScan.DataHealthSource AS s ON s.ApprovedSourceId = c.ApprovedSourceId;';
END
GO

IF OBJECT_ID(N'FileScan.CK_DataHealth_Control_DateColumnType', N'C') IS NULL
    ALTER TABLE FileScan.DataHealthControl ADD CONSTRAINT CK_DataHealth_Control_DateColumnType
        CHECK (DateColumnType IN (N'date', N'datetime', N'datetime2', N'smalldatetime', N'datetimeoffset',
                                  N'varchar', N'nvarchar', N'char', N'nchar'));
GO

-- Text columns need a list of styles. Real date columns must not have one.
-- The list is digits separated by single commas, with no empty items.
IF OBJECT_ID(N'FileScan.CK_DataHealth_Control_DateStyles', N'C') IS NULL
    ALTER TABLE FileScan.DataHealthControl ADD CONSTRAINT CK_DataHealth_Control_DateStyles
        CHECK (
            (DateColumnType IN (N'varchar', N'nvarchar', N'char', N'nchar')
                AND DateStyles IS NOT NULL
                AND LEN(DateStyles) > 0
                AND DateStyles NOT LIKE N'%[^0-9,]%'
                AND DateStyles NOT LIKE N'%,,%'
                AND DateStyles NOT LIKE N',%'
                AND DateStyles NOT LIKE N'%,')
            OR (DateColumnType NOT IN (N'varchar', N'nvarchar', N'char', N'nchar')
                AND DateStyles IS NULL)
        );
GO

-- Result: expect the two new columns on DataHealthControl, and every control to have the format of its source.
SELECT c.ControlId, c.Name, c.DateColumnType, c.DateStyles, s.SourceKey, s.DateColumnType AS SourceDateColumnType, s.DateStyles AS SourceDateStyles
FROM FileScan.DataHealthControl AS c
JOIN FileScan.DataHealthSource AS s ON s.ApprovedSourceId = c.ApprovedSourceId
ORDER BY c.ControlId;
GO
