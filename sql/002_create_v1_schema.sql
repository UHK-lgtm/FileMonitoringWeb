-- 002: V1 schema. Extends FileScan.MonitoringDefinition and adds the delivery, evidence, warning, audit and comment tables
-- described in docs/File_Monitoring_V1_Specifications.md.
-- Requires 001_create_monitoring_definition.sql to have been run first.
-- Additive and idempotent: safe to run more than once. Nothing is dropped, renamed or retyped, and existing rows and
-- columns are untouched, so FileScan and the web app keep working while and after it runs.
-- Run with:  sqlcmd -b -S <server> -d <database> -i 002_create_v1_schema.sql   (or in SSMS). The script uses GO.
-- Times are UTC in ...Utc columns (datetime2(3)). Local delivery times are time(0) in the row's TimeZoneId.
-- Who writes each table is listed in sql/CONTRACT.md.

SET NOCOUNT ON;
GO

IF OBJECT_ID(N'FileScan.MonitoringDefinition', N'U') IS NULL
    THROW 50001, N'FileScan.MonitoringDefinition not found. Run 001_create_monitoring_definition.sql first.', 1;
GO

------------------------------------------------------------------------------------------------------------------------
-- 1. FileScan.MonitoringDefinition: new configuration columns (all defaulted, so the existing row stays valid)
------------------------------------------------------------------------------------------------------------------------
-- Identity and matching
IF COL_LENGTH(N'FileScan.MonitoringDefinition', N'Provider') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD Provider nvarchar(100) NOT NULL
        CONSTRAINT DF_FileScan_MonitoringDefinition_Provider DEFAULT (N'');
IF COL_LENGTH(N'FileScan.MonitoringDefinition', N'ExpectedFileCount') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD ExpectedFileCount int NOT NULL
        CONSTRAINT DF_FileScan_MonitoringDefinition_ExpectedFileCount DEFAULT (1);

-- Schedule: Frequency is Daily, Weekly, EveryNDays, Monthly or Quarterly. The other columns apply to one frequency each.
IF COL_LENGTH(N'FileScan.MonitoringDefinition', N'Frequency') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD Frequency nvarchar(20) NOT NULL
        CONSTRAINT DF_FileScan_MonitoringDefinition_Frequency DEFAULT (N'Daily');
IF COL_LENGTH(N'FileScan.MonitoringDefinition', N'DayOfWeek') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD [DayOfWeek] tinyint NULL;        -- Weekly: 1 = Monday ... 7 = Sunday
IF COL_LENGTH(N'FileScan.MonitoringDefinition', N'IntervalDays') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD IntervalDays int NULL;           -- EveryNDays: N
IF COL_LENGTH(N'FileScan.MonitoringDefinition', N'AnchorDate') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD AnchorDate date NULL;            -- EveryNDays: a date the cycle counts from
IF COL_LENGTH(N'FileScan.MonitoringDefinition', N'DayOfMonth') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD DayOfMonth tinyint NULL;         -- Monthly, Quarterly: 1-31 (31 = last day when the month is shorter)
IF COL_LENGTH(N'FileScan.MonitoringDefinition', N'MonthOfQuarter') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD MonthOfQuarter tinyint NULL;     -- Quarterly: 1-3 (month within Jan/Apr/Jul/Oct quarter starts)

-- Delivery time, buffer and time zone. Effective deadline = ExpectedDeliveryTime + BufferMinutes, in TimeZoneId.
IF COL_LENGTH(N'FileScan.MonitoringDefinition', N'ExpectedDeliveryTime') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD ExpectedDeliveryTime time(0) NOT NULL
        CONSTRAINT DF_FileScan_MonitoringDefinition_ExpectedDeliveryTime DEFAULT ('09:00:00');
IF COL_LENGTH(N'FileScan.MonitoringDefinition', N'BufferMinutes') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD BufferMinutes int NOT NULL
        CONSTRAINT DF_FileScan_MonitoringDefinition_BufferMinutes DEFAULT (0);
IF COL_LENGTH(N'FileScan.MonitoringDefinition', N'TimeZoneId') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD TimeZoneId nvarchar(64) NOT NULL
        CONSTRAINT DF_FileScan_MonitoringDefinition_TimeZoneId DEFAULT (N'Europe/Luxembourg');

-- Filename date handling: NoDate, SameDay, PreviousDay or MonthYear, with a .NET date format such as yyyyMMdd.
IF COL_LENGTH(N'FileScan.MonitoringDefinition', N'FilenameDateMode') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD FilenameDateMode nvarchar(20) NOT NULL
        CONSTRAINT DF_FileScan_MonitoringDefinition_FilenameDateMode DEFAULT (N'NoDate');
IF COL_LENGTH(N'FileScan.MonitoringDefinition', N'FilenameDateFormat') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD FilenameDateFormat nvarchar(30) NULL;

-- Optional size check: a file outside ReferenceSizeBytes +/- SizeTolerancePercent raises a Size Warning.
IF COL_LENGTH(N'FileScan.MonitoringDefinition', N'SizeCheckEnabled') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD SizeCheckEnabled bit NOT NULL
        CONSTRAINT DF_FileScan_MonitoringDefinition_SizeCheckEnabled DEFAULT (0);
IF COL_LENGTH(N'FileScan.MonitoringDefinition', N'ReferenceSizeBytes') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD ReferenceSizeBytes bigint NULL;
IF COL_LENGTH(N'FileScan.MonitoringDefinition', N'SizeTolerancePercent') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD SizeTolerancePercent decimal(5,2) NULL;

-- Soft delete (retirement). A retired definition is never hard-deleted and must also be disabled.
IF COL_LENGTH(N'FileScan.MonitoringDefinition', N'IsRetired') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD IsRetired bit NOT NULL
        CONSTRAINT DF_FileScan_MonitoringDefinition_IsRetired DEFAULT (0);
IF COL_LENGTH(N'FileScan.MonitoringDefinition', N'RetiredAtUtc') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD RetiredAtUtc datetime2(3) NULL;
IF COL_LENGTH(N'FileScan.MonitoringDefinition', N'RetiredBy') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD RetiredBy nvarchar(256) NULL;

-- Optimistic concurrency for the web app's edits.
IF COL_LENGTH(N'FileScan.MonitoringDefinition', N'ConcurrencyToken') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD ConcurrencyToken rowversion NOT NULL;
GO

------------------------------------------------------------------------------------------------------------------------
-- 2. FileScan.MonitoringDefinition: constraints (separate batch so the new columns exist)
------------------------------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'FileScan.CK_FileScan_MonitoringDefinition_Frequency', N'C') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD CONSTRAINT CK_FileScan_MonitoringDefinition_Frequency
        CHECK (Frequency IN (N'Daily', N'Weekly', N'EveryNDays', N'Monthly', N'Quarterly'));

-- Each frequency needs its own values. NULL checks are explicit because a CHECK passes when its result is unknown.
IF OBJECT_ID(N'FileScan.CK_FileScan_MonitoringDefinition_Schedule', N'C') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD CONSTRAINT CK_FileScan_MonitoringDefinition_Schedule
        CHECK (
            Frequency = N'Daily'
            OR (Frequency = N'Weekly' AND [DayOfWeek] IS NOT NULL AND [DayOfWeek] BETWEEN 1 AND 7)
            OR (Frequency = N'EveryNDays' AND IntervalDays IS NOT NULL AND IntervalDays >= 1 AND AnchorDate IS NOT NULL)
            OR (Frequency = N'Monthly' AND DayOfMonth IS NOT NULL AND DayOfMonth BETWEEN 1 AND 31)
            OR (Frequency = N'Quarterly' AND DayOfMonth IS NOT NULL AND DayOfMonth BETWEEN 1 AND 31
                AND MonthOfQuarter IS NOT NULL AND MonthOfQuarter BETWEEN 1 AND 3));

IF OBJECT_ID(N'FileScan.CK_FileScan_MonitoringDefinition_ExpectedFileCount', N'C') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD CONSTRAINT CK_FileScan_MonitoringDefinition_ExpectedFileCount
        CHECK (ExpectedFileCount >= 1);

IF OBJECT_ID(N'FileScan.CK_FileScan_MonitoringDefinition_BufferMinutes', N'C') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD CONSTRAINT CK_FileScan_MonitoringDefinition_BufferMinutes
        CHECK (BufferMinutes BETWEEN 0 AND 1440);

IF OBJECT_ID(N'FileScan.CK_FileScan_MonitoringDefinition_FilenameDateMode', N'C') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD CONSTRAINT CK_FileScan_MonitoringDefinition_FilenameDateMode
        CHECK (FilenameDateMode IN (N'NoDate', N'SameDay', N'PreviousDay', N'MonthYear'));

IF OBJECT_ID(N'FileScan.CK_FileScan_MonitoringDefinition_FilenameDateFormat', N'C') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD CONSTRAINT CK_FileScan_MonitoringDefinition_FilenameDateFormat
        CHECK (FilenameDateMode = N'NoDate' OR FilenameDateFormat IS NOT NULL);

IF OBJECT_ID(N'FileScan.CK_FileScan_MonitoringDefinition_SizeCheck', N'C') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD CONSTRAINT CK_FileScan_MonitoringDefinition_SizeCheck
        CHECK (SizeCheckEnabled = 0
            OR (ReferenceSizeBytes IS NOT NULL AND ReferenceSizeBytes > 0
                AND SizeTolerancePercent IS NOT NULL AND SizeTolerancePercent >= 0));

IF OBJECT_ID(N'FileScan.CK_FileScan_MonitoringDefinition_Retired', N'C') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD CONSTRAINT CK_FileScan_MonitoringDefinition_Retired
        CHECK (IsRetired = 0 OR (IsEnabled = 0 AND RetiredAtUtc IS NOT NULL));
GO

------------------------------------------------------------------------------------------------------------------------
-- 3. FileScan.MonitoringDefinitionAudit: configuration change audit (written by the web API)
--    One row per changed field. Creation and retirement are recorded as FieldName '(created)' and 'IsRetired'.
------------------------------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'FileScan.MonitoringDefinitionAudit', N'U') IS NULL
    CREATE TABLE FileScan.MonitoringDefinitionAudit (
        AuditId                bigint        NOT NULL IDENTITY(1,1)
            CONSTRAINT PK_FileScan_MonitoringDefinitionAudit PRIMARY KEY,
        MonitoringDefinitionId int           NOT NULL
            CONSTRAINT FK_FileScan_MonitoringDefinitionAudit_MonitoringDefinition
                REFERENCES FileScan.MonitoringDefinition (Id),
        ChangedAtUtc           datetime2(3)  NOT NULL
            CONSTRAINT DF_FileScan_MonitoringDefinitionAudit_ChangedAtUtc DEFAULT (SYSUTCDATETIME()),
        ChangedBy              nvarchar(256) NOT NULL,   -- authenticated user
        FieldName              nvarchar(100) NOT NULL,
        OldValue               nvarchar(max) NULL,
        NewValue               nvarchar(max) NULL
    );
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FileScan_MonitoringDefinitionAudit_Definition'
               AND object_id = OBJECT_ID(N'FileScan.MonitoringDefinitionAudit'))
    CREATE INDEX IX_FileScan_MonitoringDefinitionAudit_Definition
        ON FileScan.MonitoringDefinitionAudit (MonitoringDefinitionId, ChangedAtUtc);
GO

------------------------------------------------------------------------------------------------------------------------
-- 4. FileScan.ExpectedDelivery: one logical expected delivery per definition and expected date (written by FileScan)
--    Holds the effective values that applied (snapshots) and the current state. The earlier states are in
--    DeliveryStatusTransition. A definition with no delivery on a day has no row ("Not Expected" is never stored).
------------------------------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'FileScan.ExpectedDelivery', N'U') IS NULL
    CREATE TABLE FileScan.ExpectedDelivery (
        ExpectedDeliveryId     bigint        NOT NULL IDENTITY(1,1)
            CONSTRAINT PK_FileScan_ExpectedDelivery PRIMARY KEY,
        MonitoringDefinitionId int           NOT NULL
            CONSTRAINT FK_FileScan_ExpectedDelivery_MonitoringDefinition
                REFERENCES FileScan.MonitoringDefinition (Id),
        ExpectedDate           date          NOT NULL,   -- expected delivery date in TimeZoneId
        RelativePath           nvarchar(400) NOT NULL,   -- effective values, copied from the definition when evaluated
        FileNamePattern        nvarchar(260) NOT NULL,   -- with the filename date already resolved
        ExpectedDeliveryTime   time(0)       NOT NULL,
        BufferMinutes          int           NOT NULL,
        TimeZoneId             nvarchar(64)  NOT NULL,
        DeadlineUtc            datetime2(3)  NOT NULL,   -- ExpectedDeliveryTime + BufferMinutes, converted to UTC
        ExpectedFileCount      int           NOT NULL,
        ReceivedFileCount      int           NOT NULL
            CONSTRAINT DF_FileScan_ExpectedDelivery_ReceivedFileCount DEFAULT (0),
        CurrentStatus          nvarchar(20)  NOT NULL
            CONSTRAINT DF_FileScan_ExpectedDelivery_CurrentStatus DEFAULT (N'Pending'),
        StatusChangedUtc       datetime2(3)  NOT NULL
            CONSTRAINT DF_FileScan_ExpectedDelivery_StatusChangedUtc DEFAULT (SYSUTCDATETIME()),
        CompletedAtUtc         datetime2(3)  NULL,       -- when ReceivedFileCount first reached ExpectedFileCount
        LastScannedUtc         datetime2(3)  NULL,
        CreatedUtc             datetime2(3)  NOT NULL
            CONSTRAINT DF_FileScan_ExpectedDelivery_CreatedUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_FileScan_ExpectedDelivery_Period UNIQUE (MonitoringDefinitionId, ExpectedDate),
        CONSTRAINT CK_FileScan_ExpectedDelivery_Status
            CHECK (CurrentStatus IN (N'Pending', N'OnTime', N'Missing', N'Incomplete', N'Late')),
        CONSTRAINT CK_FileScan_ExpectedDelivery_Counts
            CHECK (ExpectedFileCount >= 1 AND ReceivedFileCount >= 0)
    );
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FileScan_ExpectedDelivery_ExpectedDate'
               AND object_id = OBJECT_ID(N'FileScan.ExpectedDelivery'))
    CREATE INDEX IX_FileScan_ExpectedDelivery_ExpectedDate
        ON FileScan.ExpectedDelivery (ExpectedDate) INCLUDE (MonitoringDefinitionId, CurrentStatus);
GO

------------------------------------------------------------------------------------------------------------------------
-- 5. FileScan.DeliveryStatusTransition: significant status changes (written by FileScan, append only)
--    Example: 09:30 -> Incomplete, then 10:15 -> Late. OccurredAtUtc is when the condition became true (the deadline
--    for Missing/Incomplete, the time the last expected file arrived for OnTime/Late). Earlier rows are never removed.
------------------------------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'FileScan.DeliveryStatusTransition', N'U') IS NULL
    CREATE TABLE FileScan.DeliveryStatusTransition (
        TransitionId       bigint       NOT NULL IDENTITY(1,1)
            CONSTRAINT PK_FileScan_DeliveryStatusTransition PRIMARY KEY,
        ExpectedDeliveryId bigint       NOT NULL
            CONSTRAINT FK_FileScan_DeliveryStatusTransition_ExpectedDelivery
                REFERENCES FileScan.ExpectedDelivery (ExpectedDeliveryId),
        FromStatus         nvarchar(20) NULL,            -- NULL for the first status of a delivery
        ToStatus           nvarchar(20) NOT NULL,
        OccurredAtUtc      datetime2(3) NOT NULL,
        RecordedAtUtc      datetime2(3) NOT NULL
            CONSTRAINT DF_FileScan_DeliveryStatusTransition_RecordedAtUtc DEFAULT (SYSUTCDATETIME()),
        ExpectedFileCount  int          NOT NULL,        -- evidence as it was at the time
        ReceivedFileCount  int          NOT NULL,
        DeadlineUtc        datetime2(3) NOT NULL,
        CONSTRAINT UQ_FileScan_DeliveryStatusTransition_Event UNIQUE (ExpectedDeliveryId, ToStatus, OccurredAtUtc),
        CONSTRAINT CK_FileScan_DeliveryStatusTransition_From
            CHECK (FromStatus IS NULL OR FromStatus IN (N'Pending', N'OnTime', N'Missing', N'Incomplete', N'Late')),
        CONSTRAINT CK_FileScan_DeliveryStatusTransition_To
            CHECK (ToStatus IN (N'Pending', N'OnTime', N'Missing', N'Incomplete', N'Late'))
    );
GO

------------------------------------------------------------------------------------------------------------------------
-- 6. FileScan.FileObservation: file evidence (written by FileScan, append only)
--    A new row is added only when a file's size or timestamps change, so repeated scans do not duplicate evidence.
--    The latest row per file is the current view; earlier rows are the audit of re-uploads. IsUnexpected marks files
--    beyond the expected count; they are kept as evidence.
------------------------------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'FileScan.FileObservation', N'U') IS NULL
    CREATE TABLE FileScan.FileObservation (
        ObservationId       bigint        NOT NULL IDENTITY(1,1)
            CONSTRAINT PK_FileScan_FileObservation PRIMARY KEY,
        ExpectedDeliveryId  bigint        NOT NULL
            CONSTRAINT FK_FileScan_FileObservation_ExpectedDelivery
                REFERENCES FileScan.ExpectedDelivery (ExpectedDeliveryId),
        FileName            nvarchar(260) NOT NULL,
        RelativePath        nvarchar(400) NOT NULL,
        SizeBytes           bigint        NOT NULL,
        FileCreatedUtc      datetime2(3)  NULL,          -- where the share provides it
        FileLastModifiedUtc datetime2(3)  NULL,
        ObservedAtUtc       datetime2(3)  NOT NULL
            CONSTRAINT DF_FileScan_FileObservation_ObservedAtUtc DEFAULT (SYSUTCDATETIME()),
        IsUnexpected        bit           NOT NULL
            CONSTRAINT DF_FileScan_FileObservation_IsUnexpected DEFAULT (0),
        CONSTRAINT UQ_FileScan_FileObservation_Metadata
            UNIQUE (ExpectedDeliveryId, FileName, SizeBytes, FileCreatedUtc, FileLastModifiedUtc),
        CONSTRAINT CK_FileScan_FileObservation_SizeBytes CHECK (SizeBytes >= 0)
    );
GO

------------------------------------------------------------------------------------------------------------------------
-- 7. FileScan.DeliveryWarning: warnings, independent of delivery status (written by FileScan)
--    SizeWarning is per file (FileName set). UnexpectedFileWarning is per delivery (FileName empty); the extra files
--    are in FileObservation with IsUnexpected = 1. ResolvedAtUtc is set when the condition no longer applies.
------------------------------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'FileScan.DeliveryWarning', N'U') IS NULL
    CREATE TABLE FileScan.DeliveryWarning (
        WarningId          bigint        NOT NULL IDENTITY(1,1)
            CONSTRAINT PK_FileScan_DeliveryWarning PRIMARY KEY,
        ExpectedDeliveryId bigint        NOT NULL
            CONSTRAINT FK_FileScan_DeliveryWarning_ExpectedDelivery
                REFERENCES FileScan.ExpectedDelivery (ExpectedDeliveryId),
        WarningType        nvarchar(30)  NOT NULL,
        FileName           nvarchar(260) NOT NULL
            CONSTRAINT DF_FileScan_DeliveryWarning_FileName DEFAULT (N''),
        Detail             nvarchar(400) NULL,
        DetectedAtUtc      datetime2(3)  NOT NULL
            CONSTRAINT DF_FileScan_DeliveryWarning_DetectedAtUtc DEFAULT (SYSUTCDATETIME()),
        ResolvedAtUtc      datetime2(3)  NULL,
        CONSTRAINT UQ_FileScan_DeliveryWarning_Condition UNIQUE (ExpectedDeliveryId, WarningType, FileName),
        CONSTRAINT CK_FileScan_DeliveryWarning_Type CHECK (WarningType IN (N'SizeWarning', N'UnexpectedFileWarning'))
    );
GO

------------------------------------------------------------------------------------------------------------------------
-- 8. FileScan.IncidentComment: comments and reasons on a delivery (written by the web API, append only)
--    FileScan never writes or changes this table.
------------------------------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'FileScan.IncidentComment', N'U') IS NULL
    CREATE TABLE FileScan.IncidentComment (
        CommentId          bigint         NOT NULL IDENTITY(1,1)
            CONSTRAINT PK_FileScan_IncidentComment PRIMARY KEY,
        ExpectedDeliveryId bigint         NOT NULL
            CONSTRAINT FK_FileScan_IncidentComment_ExpectedDelivery
                REFERENCES FileScan.ExpectedDelivery (ExpectedDeliveryId),
        CommentText        nvarchar(2000) NOT NULL,
        CreatedBy          nvarchar(256)  NOT NULL,      -- authenticated user
        CreatedAtUtc       datetime2(3)   NOT NULL
            CONSTRAINT DF_FileScan_IncidentComment_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_FileScan_IncidentComment_Text CHECK (LEN(LTRIM(RTRIM(CommentText))) > 0)
    );
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FileScan_IncidentComment_Delivery'
               AND object_id = OBJECT_ID(N'FileScan.IncidentComment'))
    CREATE INDEX IX_FileScan_IncidentComment_Delivery
        ON FileScan.IncidentComment (ExpectedDeliveryId, CreatedAtUtc);
GO

------------------------------------------------------------------------------------------------------------------------
-- 9. Permissions (commented: fill in the database users the Web App and the Function App connect as, then run)
--    Skip this section if they use db_datareader / db_datawriter, which already cover the new tables.
------------------------------------------------------------------------------------------------------------------------
-- GRANT SELECT, INSERT, UPDATE ON FileScan.MonitoringDefinition      TO [<web-app-user>];
-- GRANT SELECT, INSERT         ON FileScan.MonitoringDefinitionAudit TO [<web-app-user>];
-- GRANT SELECT, INSERT         ON FileScan.IncidentComment           TO [<web-app-user>];
-- GRANT SELECT ON FileScan.ExpectedDelivery, FileScan.DeliveryStatusTransition, FileScan.FileObservation,
--                 FileScan.DeliveryWarning                            TO [<web-app-user>];
--
-- GRANT SELECT ON FileScan.MonitoringDefinition                       TO [<function-app-user>];
-- GRANT SELECT, INSERT, UPDATE ON FileScan.ExpectedDelivery           TO [<function-app-user>];
-- GRANT SELECT, INSERT ON FileScan.DeliveryStatusTransition           TO [<function-app-user>];
-- GRANT SELECT, INSERT ON FileScan.FileObservation                    TO [<function-app-user>];
-- GRANT SELECT, INSERT, UPDATE ON FileScan.DeliveryWarning            TO [<function-app-user>];
GO

------------------------------------------------------------------------------------------------------------------------
-- Result: expect 7 tables in schema FileScan and 27 columns on MonitoringDefinition.
------------------------------------------------------------------------------------------------------------------------
SELECT t.name AS TableName
FROM sys.tables AS t JOIN sys.schemas AS s ON s.schema_id = t.schema_id
WHERE s.name = N'FileScan'
ORDER BY t.name;

SELECT COUNT(*) AS MonitoringDefinitionColumns
FROM sys.columns WHERE object_id = OBJECT_ID(N'FileScan.MonitoringDefinition');
GO
