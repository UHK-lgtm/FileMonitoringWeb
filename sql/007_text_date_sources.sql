-- 007: text-date sources for Data Health. Requires 006 (already applied).
-- Updates FileScan.DataHealthSource so that a source whose date column is stored as text (varchar, nvarchar, char, nchar)
-- can be counted. Additive and idempotent: existing rows keep DateColumnType 'date' and a NULL DateStyles.
--
-- DateColumnType: the stored type of the date column: date, datetime, datetime2, smalldatetime, datetimeoffset,
--                 varchar, nvarchar, char or nchar.
-- DateStyles:     for text columns only. An ORDERED comma list of SQL Server TRY_CONVERT styles, tried in turn.
--                 Example: 112,23,103 tries yyyyMMdd, then yyyy-MM-dd, then dd/MM/yyyy.
--                 A value that matches none of the styles is counted as unreadable, never silently dropped.
--                 Order matters for ambiguous formats: 103 (dd/MM/yyyy) and 101 (MM/dd/yyyy) read the same
--                 text differently, so list only the styles your data really uses, in the order they should win.
--
-- Run with:  sqlcmd -b -S <server> -d <database> -G -i 007_text_date_sources.sql

SET NOCOUNT ON;
GO

IF COL_LENGTH(N'FileScan.DataHealthSource', N'DateColumnType') IS NULL
    ALTER TABLE FileScan.DataHealthSource ADD DateColumnType nvarchar(20) NOT NULL
        CONSTRAINT DF_DataHealth_Source_DateColumnType DEFAULT (N'date');
IF COL_LENGTH(N'FileScan.DataHealthSource', N'DateStyles') IS NULL
    ALTER TABLE FileScan.DataHealthSource ADD DateStyles nvarchar(100) NULL;
GO

IF OBJECT_ID(N'FileScan.CK_DataHealth_Source_DateColumnType', N'C') IS NULL
    ALTER TABLE FileScan.DataHealthSource ADD CONSTRAINT CK_DataHealth_Source_DateColumnType
        CHECK (DateColumnType IN (N'date', N'datetime', N'datetime2', N'smalldatetime', N'datetimeoffset',
                                  N'varchar', N'nvarchar', N'char', N'nchar'));
GO

-- Text columns need a list of styles. Real date columns must not have one.
-- The list is digits separated by single commas, with no empty items.
IF OBJECT_ID(N'FileScan.CK_DataHealth_Source_DateStyles', N'C') IS NULL
    ALTER TABLE FileScan.DataHealthSource ADD CONSTRAINT CK_DataHealth_Source_DateStyles
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

-- Result: expect the two new columns on DataHealthSource.
SELECT c.name AS ColumnName, t.name AS DataType
FROM sys.columns AS c
JOIN sys.types AS t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID(N'FileScan.DataHealthSource')
  AND c.name IN (N'DateColumnType', N'DateStyles');
GO
