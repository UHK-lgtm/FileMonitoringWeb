-- 005: FileScan.ScanRun. One row per production scan run (ProductionScan timer).
-- Written by FileScan (Function App). Read by the web app for the "Last scan" line.
-- Additive and idempotent: safe to run more than once. Nothing existing is changed.
-- Requires 001 and 002. Run with:  sqlcmd -b -S <server> -d <database> -i 005_create_scan_run.sql   (or in SSMS). The script uses GO.
-- Times are UTC (datetime2(3)). FailureType holds the exception type name only, never the message.
-- Grants are not in this script. See sql/CONTRACT.md, "Grants needed".

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'FileScan')
    EXEC (N'CREATE SCHEMA FileScan AUTHORIZATION dbo');
GO

IF OBJECT_ID(N'FileScan.ScanRun', N'U') IS NULL
BEGIN
    CREATE TABLE FileScan.ScanRun (
        ScanRunId      bigint        NOT NULL IDENTITY(1,1) CONSTRAINT PK_FileScan_ScanRun PRIMARY KEY,
        StartedUtc     datetime2(3)  NOT NULL CONSTRAINT DF_FileScan_ScanRun_StartedUtc DEFAULT (SYSUTCDATETIME()),
        FinishedUtc    datetime2(3)  NULL,
        Status         nvarchar(20)  NOT NULL,
        TriggerType    nvarchar(20)  NOT NULL CONSTRAINT DF_FileScan_ScanRun_TriggerType DEFAULT (N'Timer'),
        ScheduledToday int           NULL,
        OpenDeliveries int           NULL,
        StatusChanges  int           NULL,
        FailureStage   nvarchar(60)  NULL,
        FailureType    nvarchar(200) NULL,
        InvocationId   nvarchar(64)  NULL,
        CONSTRAINT CK_FileScan_ScanRun_Status CHECK (Status IN (N'Running', N'Succeeded', N'Failed')),
        CONSTRAINT CK_FileScan_ScanRun_TriggerType CHECK (TriggerType IN (N'Timer', N'Manual'))
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FileScan_ScanRun_StartedUtc' AND object_id = OBJECT_ID(N'FileScan.ScanRun'))
    CREATE INDEX IX_FileScan_ScanRun_StartedUtc ON FileScan.ScanRun (StartedUtc DESC);
GO
