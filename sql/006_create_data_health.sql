-- 006: Data Health schema. Requires 001-005 to be applied first (independent of the FileScan tables).
-- Additive and idempotent: safe to run more than once. Nothing is dropped or retyped.
--
-- DataHealth.ApprovedSource is the only list of tables the web app may count. It is filled by an admin script
-- (see the seed template in this folder, 006_seed_data_health_sources_TEMPLATE.sql). The web app never writes to it,
-- so users can only pick from this list. Controls reference a source; they never name a table directly.
--
-- Run with:  sqlcmd -b -S <server> -d <database> -G -i 006_create_data_health.sql

SET NOCOUNT ON;
GO

IF SCHEMA_ID(N'DataHealth') IS NULL
    EXEC (N'CREATE SCHEMA DataHealth AUTHORIZATION dbo');
GO

IF OBJECT_ID(N'DataHealth.ApprovedSource', N'U') IS NULL
    CREATE TABLE DataHealth.ApprovedSource (
        ApprovedSourceId int           NOT NULL IDENTITY(1,1)
            CONSTRAINT PK_DataHealth_ApprovedSource PRIMARY KEY,
        SourceKey        nvarchar(60)  NOT NULL
            CONSTRAINT UQ_DataHealth_ApprovedSource_SourceKey UNIQUE,
        DisplayName      nvarchar(200) NOT NULL,
        SchemaName       nvarchar(128) NOT NULL,
        ObjectName       nvarchar(128) NOT NULL,
        DateColumn       nvarchar(128) NOT NULL,
        IsActive         bit           NOT NULL
            CONSTRAINT DF_DataHealth_ApprovedSource_IsActive DEFAULT (1),
        CreatedUtc       datetime2(3)  NOT NULL
            CONSTRAINT DF_DataHealth_ApprovedSource_CreatedUtc DEFAULT (SYSUTCDATETIME())
    );
GO

IF OBJECT_ID(N'DataHealth.Control', N'U') IS NULL
    CREATE TABLE DataHealth.Control (
        ControlId                 int           NOT NULL IDENTITY(1,1)
            CONSTRAINT PK_DataHealth_Control PRIMARY KEY,
        Name                      nvarchar(200) NOT NULL,
        DisplayOrder              int           NOT NULL
            CONSTRAINT DF_DataHealth_Control_DisplayOrder DEFAULT (0),
        IsEnabled                 bit           NOT NULL
            CONSTRAINT DF_DataHealth_Control_IsEnabled DEFAULT (1),
        ApprovedSourceId          int           NOT NULL
            CONSTRAINT FK_DataHealth_Control_ApprovedSource REFERENCES DataHealth.ApprovedSource (ApprovedSourceId),
        OffsetDays                int           NOT NULL
            CONSTRAINT DF_DataHealth_Control_OffsetDays DEFAULT (2),
        OffsetType                nvarchar(20)  NOT NULL
            CONSTRAINT DF_DataHealth_Control_OffsetType DEFAULT (N'Weekdays'),
        HistoryDays               int           NOT NULL
            CONSTRAINT DF_DataHealth_Control_HistoryDays DEFAULT (10),
        MissingCheck              bit           NOT NULL
            CONSTRAINT DF_DataHealth_Control_MissingCheck DEFAULT (1),
        DecreaseCheck             bit           NOT NULL
            CONSTRAINT DF_DataHealth_Control_DecreaseCheck DEFAULT (1),
        DecreaseTolerancePercent  decimal(5,2)  NOT NULL
            CONSTRAINT DF_DataHealth_Control_Tolerance DEFAULT (0),
        IsRetired                 bit           NOT NULL
            CONSTRAINT DF_DataHealth_Control_IsRetired DEFAULT (0),
        RetiredAtUtc              datetime2(3)  NULL,
        RetiredBy                 nvarchar(256) NULL,
        CreatedUtc                datetime2(3)  NOT NULL
            CONSTRAINT DF_DataHealth_Control_CreatedUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedUtc                datetime2(3)  NOT NULL
            CONSTRAINT DF_DataHealth_Control_UpdatedUtc DEFAULT (SYSUTCDATETIME()),
        ConcurrencyToken          rowversion    NOT NULL,
        CONSTRAINT CK_DataHealth_Control_OffsetDays CHECK (OffsetDays BETWEEN 0 AND 60),
        CONSTRAINT CK_DataHealth_Control_OffsetType CHECK (OffsetType IN (N'CalendarDays', N'Weekdays')),
        CONSTRAINT CK_DataHealth_Control_HistoryDays CHECK (HistoryDays BETWEEN 1 AND 365),
        CONSTRAINT CK_DataHealth_Control_Tolerance CHECK (DecreaseTolerancePercent BETWEEN 0 AND 100),
        CONSTRAINT CK_DataHealth_Control_Retired CHECK (IsRetired = 0 OR (IsEnabled = 0 AND RetiredAtUtc IS NOT NULL))
    );
GO

IF OBJECT_ID(N'DataHealth.ControlAudit', N'U') IS NULL
    CREATE TABLE DataHealth.ControlAudit (
        AuditId     bigint        NOT NULL IDENTITY(1,1)
            CONSTRAINT PK_DataHealth_ControlAudit PRIMARY KEY,
        ControlId   int           NOT NULL
            CONSTRAINT FK_DataHealth_ControlAudit_Control REFERENCES DataHealth.Control (ControlId),
        ChangedAtUtc datetime2(3) NOT NULL
            CONSTRAINT DF_DataHealth_ControlAudit_ChangedAtUtc DEFAULT (SYSUTCDATETIME()),
        ChangedBy   nvarchar(256) NOT NULL,
        FieldName   nvarchar(100) NOT NULL,
        OldValue    nvarchar(max) NULL,
        NewValue    nvarchar(max) NULL
    );
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DataHealth_ControlAudit_Control'
               AND object_id = OBJECT_ID(N'DataHealth.ControlAudit'))
    CREATE INDEX IX_DataHealth_ControlAudit_Control ON DataHealth.ControlAudit (ControlId, ChangedAtUtc);
GO

-- Result: expect the three tables in schema DataHealth.
SELECT t.name AS TableName
FROM sys.tables AS t JOIN sys.schemas AS s ON s.schema_id = t.schema_id
WHERE s.name = N'DataHealth'
ORDER BY t.name;
GO
