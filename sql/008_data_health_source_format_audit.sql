-- 008: format settings for approved sources can be changed from the web app, with an audit trail.
-- Requires 006 and 007. Additive and idempotent.
--
-- FileScan.DataHealthSourceAudit: one row per changed field of an approved source (who, when, old value, new value).
-- FileScan.DataHealthSource.ConcurrencyToken: detects that someone else changed the source since it was read.
-- The web app may change only DateColumnType and DateStyles of an existing source. It cannot add a source or change
-- its schema, table, view or column name. Those stay with the admin script.
--
-- Run with:  sqlcmd -b -S <server> -d <database> -G -i 008_data_health_source_format_audit.sql

SET NOCOUNT ON;
GO

IF COL_LENGTH(N'FileScan.DataHealthSource', N'ConcurrencyToken') IS NULL
    ALTER TABLE FileScan.DataHealthSource ADD ConcurrencyToken rowversion NOT NULL;
GO

IF OBJECT_ID(N'FileScan.DataHealthSourceAudit', N'U') IS NULL
    CREATE TABLE FileScan.DataHealthSourceAudit (
        AuditId         bigint        NOT NULL IDENTITY(1,1)
            CONSTRAINT PK_DataHealth_SourceAudit PRIMARY KEY,
        ApprovedSourceId int          NOT NULL
            CONSTRAINT FK_DataHealth_SourceAudit_Source REFERENCES FileScan.DataHealthSource (ApprovedSourceId),
        ChangedAtUtc    datetime2(3)  NOT NULL
            CONSTRAINT DF_DataHealth_SourceAudit_ChangedAtUtc DEFAULT (SYSUTCDATETIME()),
        ChangedBy       nvarchar(256) NOT NULL,
        FieldName       nvarchar(100) NOT NULL,
        OldValue        nvarchar(max) NULL,
        NewValue        nvarchar(max) NULL
    );
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DataHealth_SourceAudit_Source'
               AND object_id = OBJECT_ID(N'FileScan.DataHealthSourceAudit'))
    CREATE INDEX IX_DataHealth_SourceAudit_Source ON FileScan.DataHealthSourceAudit (ApprovedSourceId, ChangedAtUtc);
GO

-- Result: expect the audit table and the concurrency token column.
SELECT OBJECT_NAME(object_id) AS TableName
FROM sys.tables
WHERE name IN (N'DataHealthSourceAudit') AND SCHEMA_ID(N'FileScan') = schema_id
UNION ALL
SELECT N'DataHealthSource.ConcurrencyToken'
WHERE COL_LENGTH(N'FileScan.DataHealthSource', N'ConcurrencyToken') IS NOT NULL;
GO
