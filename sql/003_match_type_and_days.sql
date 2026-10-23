-- 003: how file names are matched, and which weekdays a delivery is expected on.
-- Requires 002. Additive and idempotent: nothing is dropped or retyped.
--
-- MatchType (definitions and expected deliveries):
--   Wildcard  * and ? wildcards, matched case-insensitively. The original rule, and the default.
--   Regex     A regular expression that must match the whole file name. Used by the imported production flows.
-- DaysOfWeek (definitions): ISO weekdays the delivery is expected on, as a comma list such as 1,2,3,4,5
--   (1 = Monday ... 7 = Sunday). NULL means every day, which is the existing behaviour.
-- ExpectedDelivery.MatchType is a snapshot taken when the delivery is planned, like its other effective values.
--
-- Run with:  sqlcmd -b -S <server> -d <database> -G -i 003_match_type_and_days.sql

SET NOCOUNT ON;
GO

IF COL_LENGTH(N'FileScan.MonitoringDefinition', N'MatchType') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD MatchType nvarchar(10) NOT NULL
        CONSTRAINT DF_FileScan_MonitoringDefinition_MatchType DEFAULT (N'Wildcard');
IF COL_LENGTH(N'FileScan.MonitoringDefinition', N'DaysOfWeek') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD DaysOfWeek nvarchar(20) NULL;
IF COL_LENGTH(N'FileScan.ExpectedDelivery', N'MatchType') IS NULL
    ALTER TABLE FileScan.ExpectedDelivery ADD MatchType nvarchar(10) NOT NULL
        CONSTRAINT DF_FileScan_ExpectedDelivery_MatchType DEFAULT (N'Wildcard');
GO

IF OBJECT_ID(N'FileScan.CK_FileScan_MonitoringDefinition_MatchType', N'C') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD CONSTRAINT CK_FileScan_MonitoringDefinition_MatchType
        CHECK (MatchType IN (N'Wildcard', N'Regex'));
GO

-- A comma list of digits 1-7. No other characters, no empty items, no leading or trailing comma.
IF OBJECT_ID(N'FileScan.CK_FileScan_MonitoringDefinition_DaysOfWeek', N'C') IS NULL
    ALTER TABLE FileScan.MonitoringDefinition ADD CONSTRAINT CK_FileScan_MonitoringDefinition_DaysOfWeek
        CHECK (DaysOfWeek IS NULL
            OR (LEN(DaysOfWeek) > 0
                AND DaysOfWeek NOT LIKE N'%[^1-7,]%'
                AND DaysOfWeek NOT LIKE N'%,,%'
                AND DaysOfWeek NOT LIKE N',%'
                AND DaysOfWeek NOT LIKE N'%,'));
GO

IF OBJECT_ID(N'FileScan.CK_FileScan_ExpectedDelivery_MatchType', N'C') IS NULL
    ALTER TABLE FileScan.ExpectedDelivery ADD CONSTRAINT CK_FileScan_ExpectedDelivery_MatchType
        CHECK (MatchType IN (N'Wildcard', N'Regex'));
GO

-- Result: expect MatchType and DaysOfWeek on MonitoringDefinition (both present).
SELECT COUNT(*) AS MatchingColumns
FROM sys.columns
WHERE object_id = OBJECT_ID(N'FileScan.MonitoringDefinition') AND name IN (N'MatchType', N'DaysOfWeek');
GO
