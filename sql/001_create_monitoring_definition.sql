-- Schema FileScan: monitoring definitions read by the FileScannerFunctionApp Function App.
-- Table FileScan.MonitoringDefinition
--   Id               Identity key. The test endpoint (ConfigurationDrivenScanTestHttp) loads Id 1.
--   Name             Display name.
--   RelativePath     Folder relative to the generalisa share root. '' means the root. Uses '/' separators, no leading or
--                    trailing '/', no '.' or '..' segments, no ':' and no '\'. Other values are rejected by the scanner.
--   FileNamePattern  File name only, with * and ? wildcards, matched case-insensitively. No '/' or '\'.
--   IsEnabled        Only enabled rows are read by the scanner.
--   CreatedUtc       UTC time the row was created.
--   UpdatedUtc       UTC time the row was last changed by the seed script.
-- The storage account, share and credentials are not stored here. They come from Function App settings.
--
-- Idempotent: safe to run more than once. Run the whole script in one session (SSMS or sqlcmd).

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'FileScan')
    EXEC (N'CREATE SCHEMA FileScan AUTHORIZATION dbo');

IF OBJECT_ID(N'FileScan.MonitoringDefinition', N'U') IS NULL
    EXEC (N'
CREATE TABLE FileScan.MonitoringDefinition (
    Id              int           NOT NULL IDENTITY(1,1) CONSTRAINT PK_FileScan_MonitoringDefinition PRIMARY KEY,
    Name            nvarchar(200) NOT NULL,
    RelativePath    nvarchar(400) NOT NULL CONSTRAINT DF_FileScan_MonitoringDefinition_RelativePath DEFAULT (N''''),
    FileNamePattern nvarchar(260) NOT NULL,
    IsEnabled       bit           NOT NULL CONSTRAINT DF_FileScan_MonitoringDefinition_IsEnabled DEFAULT (1),
    CreatedUtc      datetime2(3)  NOT NULL CONSTRAINT DF_FileScan_MonitoringDefinition_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    UpdatedUtc      datetime2(3)  NOT NULL CONSTRAINT DF_FileScan_MonitoringDefinition_UpdatedUtc DEFAULT (SYSUTCDATETIME())
);');

-- Test definition for the vertical slice: the BNP Fund Accounting test file in generalisa.
-- If the earlier ProviderA test row exists, it is converted to this definition, so the endpoint's Id stays 1.
-- Change FileNamePattern to a non-existing name for the zero-match test.
EXEC (N'
IF EXISTS (SELECT 1 FROM FileScan.MonitoringDefinition WHERE Name = N''Test: ProviderA example.csv'')
    UPDATE FileScan.MonitoringDefinition
    SET Name = N''BNP Fund Accounting Test'',
        RelativePath = N''BNP_FUND_ACCOUNTING/IN/PRD/test'',
        FileNamePattern = N''LU_PO_FIO_20260112.xlsx'',
        IsEnabled = 1,
        UpdatedUtc = SYSUTCDATETIME()
    WHERE Name = N''Test: ProviderA example.csv'';
ELSE IF NOT EXISTS (SELECT 1 FROM FileScan.MonitoringDefinition WHERE Name = N''BNP Fund Accounting Test'')
    INSERT INTO FileScan.MonitoringDefinition (Name, RelativePath, FileNamePattern, IsEnabled)
    VALUES (N''BNP Fund Accounting Test'', N''BNP_FUND_ACCOUNTING/IN/PRD/test'', N''LU_PO_FIO_20260112.xlsx'', 1);');
