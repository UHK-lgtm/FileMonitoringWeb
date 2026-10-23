-- 004: production monitoring definitions, from the flow list (62 flows).
-- Requires 003.
--
-- Every flow is inserted DISABLED (IsEnabled = 0). Nothing is planned or scanned until a person has reviewed the row
-- and enabled it in the web app. Do not enable a flow before the scanner reads MatchType and DaysOfWeek (see sql/README.md).
--
-- Insert only: a flow is added when no row with the same Name exists. Existing rows are never changed, so later
-- edits made in the web app are not overwritten. Re-running the script adds nothing new.
--
-- Mapping from the flow list:
--   FlowName          -> Name
--   Category          -> Provider
--   ProductionPath    -> RelativePath (backslashes become '/')
--   Regex             -> FileNamePattern, with MatchType = Regex
--   ExpectedCount     -> ExpectedFileCount
--   BeginningTime     -> ExpectedDeliveryTime
--   EndTime           -> deadline, as BufferMinutes = EndTime - BeginningTime
--   DaysOfTheWeek     -> DaysOfWeek (ISO numbers: Monday = 1 ... Sunday = 7)
-- Time zone: Europe/Luxembourg. Filename date mode: NoDate (the regexes already contain the date).

SET NOCOUNT ON;
GO

MERGE FileScan.MonitoringDefinition AS target
USING (VALUES
    -- Bloomberg
    (N'Bloomberg ISIN GIL', N'Bloomberg', N'bloomberg/IN/PRD', N'ISINGIL_\d{8}\.out', N'Regex', 1, CAST(N'01:00' AS time(0)), 120, N'1,2,3,4,5'),
    (N'Bloomberg ISIN GIL PR', N'Bloomberg', N'bloomberg/IN/PRD', N'ISINGILPR_\d{8}\.out', N'Regex', 1, CAST(N'01:00' AS time(0)), 120, N'1,2,3,4,5'),
    (N'Bloomberg Ticker GIL', N'Bloomberg', N'bloomberg/IN/PRD', N'TICKERGIL_\d{8}\.out', N'Regex', 1, CAST(N'01:00' AS time(0)), 120, N'1,2,3,4,5'),
    (N'Bloomberg Ticker GIL PR', N'Bloomberg', N'bloomberg/IN/PRD', N'TICKERGILPR_\d{8}\.out', N'Regex', 1, CAST(N'01:00' AS time(0)), 120, N'1,2,3,4,5'),

    -- BNP Custody
    (N'BNP Daily Movements', N'BNP Custody', N'bnp_custody/IN/PRD', N'BNP_LU_Daily_MovementsL_\d{8}\.csv', N'Regex', 1, CAST(N'01:00' AS time(0)), 300, N'1,2,3,4,5'),
    (N'BNP Daily Positions', N'BNP Custody', N'bnp_custody/IN/PRD', N'BNP_LU_Daily_Positions2L_\d{8}\.csv', N'Regex', 1, CAST(N'05:00' AS time(0)), 60, N'1,2,3,4,5'),
    (N'BNP BP2S Reporting', N'BNP Custody', N'bnp_custody/IN/PRD', N'BP2S_REPORTING_\d{8}\.zip', N'Regex', 1, CAST(N'05:00' AS time(0)), 60, N'1,2,3,4,5'),
    (N'BNP Collateral', N'BNP Custody', N'bnp_custody/IN/PRD', N'Collateral_\d{8}\.csv', N'Regex', 1, CAST(N'17:00' AS time(0)), 120, N'1,2,3,4,5'),
    (N'BNP Daily', N'BNP Custody', N'bnp_custody/IN/PRD', N'Daily_\d{8}\.csv', N'Regex', 1, CAST(N'17:00' AS time(0)), 120, N'1,2,3,4,5'),
    (N'BNP Pending Trade', N'BNP Custody', N'bnp_custody/IN/PRD', N'PendingTrade_\d{8}\.csv', N'Regex', 1, CAST(N'17:00' AS time(0)), 120, N'1,2,3,4,5'),
    (N'BNP Trade Details', N'BNP Custody', N'bnp_custody/IN/PRD', N'TradeDetails_\d{8}\.csv', N'Regex', 1, CAST(N'17:00' AS time(0)), 120, N'1,2,3,4,5'),

    -- BNP Fund Accounting
    (N'BNP DE DSR', N'BNP Fund Accounting', N'bnp_fund_accounting/IN/PRD', N'DE_PO_DSR_\d{8}.*\.xlsx', N'Regex', 6, CAST(N'11:00' AS time(0)), 300, N'1,2,3,4,5'),
    (N'BNP DE FIO', N'BNP Fund Accounting', N'bnp_fund_accounting/IN/PRD', N'DE_PO_FIO_\d{8}.*\.xlsx', N'Regex', 6, CAST(N'11:00' AS time(0)), 300, N'1,2,3,4,5'),
    (N'BNP DE NAV', N'BNP Fund Accounting', N'bnp_fund_accounting/IN/PRD', N'DE_PO_NAV_\d{8}\.csv', N'Regex', 1, CAST(N'15:00' AS time(0)), 60, N'1,2,3,4,5'),
    (N'BNP DE RFS', N'BNP Fund Accounting', N'bnp_fund_accounting/IN/PRD', N'DE_PO_RFS_\d{8}\.csv', N'Regex', 1, CAST(N'15:00' AS time(0)), 60, N'1,2,3,4,5'),
    (N'BNP DE TRA', N'BNP Fund Accounting', N'bnp_fund_accounting/IN/PRD', N'DE_PO_TRA_\d{8}.*\.xlsx', N'Regex', 6, CAST(N'11:00' AS time(0)), 300, N'1,2,3,4,5'),
    (N'BNP LU DSR', N'BNP Fund Accounting', N'bnp_fund_accounting/IN/PRD', N'LU_PO_DSR_\d{8}\.xlsx', N'Regex', 1, CAST(N'15:00' AS time(0)), 60, N'1,2,3,4,5'),
    (N'BNP LU FIO', N'BNP Fund Accounting', N'bnp_fund_accounting/IN/PRD', N'LU_PO_FIO_\d{8}\.xlsx', N'Regex', 1, CAST(N'15:00' AS time(0)), 60, N'1,2,3,4,5'),
    (N'BNP LU NAV', N'BNP Fund Accounting', N'bnp_fund_accounting/IN/PRD', N'LU_PO_NAV_\d{8}\.csv', N'Regex', 1, CAST(N'15:00' AS time(0)), 60, N'1,2,3,4,5'),
    (N'BNP LU RFS', N'BNP Fund Accounting', N'bnp_fund_accounting/IN/PRD', N'LU_PO_RFS_\d{8}\.csv', N'Regex', 1, CAST(N'15:00' AS time(0)), 60, N'1,2,3,4,5'),
    (N'BNP LU TRA', N'BNP Fund Accounting', N'bnp_fund_accounting/IN/PRD', N'LU_PO_TRA_\d{8}\.xlsx', N'Regex', 1, CAST(N'15:00' AS time(0)), 60, N'1,2,3,4,5'),
    (N'BNP Exchange Rates', N'BNP Fund Accounting', N'bnp_fund_accounting/IN/PRD', N'EXCHANGE_RATES_.*\.XLSX', N'Regex', 1, CAST(N'14:00' AS time(0)), 60, N'1,2,3,4,5'),
    (N'BNP NAV Data FA', N'BNP Fund Accounting', N'bnp_fund_accounting/IN/PRD', N'NAV_DATA_FA.*\.XLSX', N'Regex', 6, CAST(N'11:00' AS time(0)), 60, N'1,2,3,4,5'),
    (N'BNP NAV Pack FA', N'BNP Fund Accounting', N'bnp_fund_accounting/IN/PRD', N'NAV_PACK_FA.*\.xlsx', N'Regex', 6, CAST(N'11:00' AS time(0)), 60, N'1,2,3,4,5'),
    (N'BNP NAV Pack LG', N'BNP Fund Accounting', N'bnp_fund_accounting/IN/PRD', N'NAV_PACK_LG.*\.xlsx', N'Regex', 50, CAST(N'12:00' AS time(0)), 240, N'1,2,3,4,5'),
    (N'BNP Perf Fees History', N'BNP Fund Accounting', N'bnp_fund_accounting/IN/PRD', N'Perf_fees_His_.*\.XLSX', N'Regex', 9, CAST(N'13:00' AS time(0)), 180, N'1,2,3,4,5'),

    -- BNP Transfer Agent
    (N'BNP TA Clients Address', N'BNP Transfer Agent', N'bnp_transfer_agent/IN/PRD', N'\d{8}_CLIENTS_ADDRESS\.txt', N'Regex', 1, CAST(N'23:00' AS time(0)), 59, N'1,2,3,4,5'),
    (N'BNP TA Clients Document', N'BNP Transfer Agent', N'bnp_transfer_agent/IN/PRD', N'\d{8}_CLIENTS_DOCUMENT\.txt', N'Regex', 1, CAST(N'23:00' AS time(0)), 59, N'1,2,3,4,5'),
    (N'BNP TA Entity Relations', N'BNP Transfer Agent', N'bnp_transfer_agent/IN/PRD', N'\d{8}_CLIENTS_ENTITY_.*\.txt', N'Regex', 3, CAST(N'23:00' AS time(0)), 59, N'1,2,3,4,5'),
    (N'BNP TA Clients Party', N'BNP Transfer Agent', N'bnp_transfer_agent/IN/PRD', N'\d{8}_CLIENTS_PARTY\.txt', N'Regex', 1, CAST(N'23:00' AS time(0)), 59, N'1,2,3,4,5'),
    (N'BNP TA Holdings DLD', N'BNP Transfer Agent', N'bnp_transfer_agent/IN/PRD', N'DLD_HOLDINGS.*\.csv', N'Regex', 1, CAST(N'15:00' AS time(0)), 60, N'1,2,3,4,5'),
    (N'BNP TA Transactions DLD', N'BNP Transfer Agent', N'bnp_transfer_agent/IN/PRD', N'DLD_TRANS_.*\.csv', N'Regex', 1, CAST(N'15:00' AS time(0)), 60, N'1,2,3,4,5'),

    -- Other providers
    (N'DTCC Reports', N'DTCC', N'DTCC/IN/PRD', N'DTS3\.DOWN\..*', N'Regex', 600, CAST(N'00:00' AS time(0)), 1439, N'1,2,3,4,5,6,7'),
    (N'Fundsquare Reports', N'Fundsquare', N'fundsquare/IN/PRD', N'.*', N'Regex', 1, CAST(N'00:00' AS time(0)), 1439, N'1,2,3,4,5'),
    (N'Lumyna Files', N'Lumyna', N'lumyna/IN/PRD', N'.*', N'Regex', 30, CAST(N'00:00' AS time(0)), 1439, N'1,2,3,4,5'),
    (N'RiskConcile TPT Reports', N'RiskConcile', N'riskconcile/IN/PRD', N'.*_TPT_.*\.xlsx', N'Regex', 168, CAST(N'12:00' AS time(0)), 120, N'1,2,3,4,5'),
    (N'SFTR SecLending Reports', N'SFTR Reports', N'SFTRReports/IN/PRD/SecLending', N'.*', N'Regex', 2, CAST(N'00:00' AS time(0)), 1439, N'1,2,3,4,5'),
    (N'SFTR TRS Reports', N'SFTR Reports', N'SFTRReports/IN/PRD/TRS', N'.*', N'Regex', 2, CAST(N'00:00' AS time(0)), 1439, N'1,2,3,4,5'),

    -- State Street
    (N'SST Position SLH111 LUM', N'State Street Position', N'statestreet/IN/PRD', N'GIL_BO_SLH111_LUM.*\.csv', N'Regex', 2, CAST(N'08:00' AS time(0)), 870, N'1,2,3,4,5'),
    (N'SST Position SLH111 GIL', N'State Street Position', N'statestreet/IN/PRD', N'GIL_BO_SLH111_GIL.*\.csv', N'Regex', 1, CAST(N'14:00' AS time(0)), 120, N'1,2,3,4,5'),
    (N'SST GLA105', N'State Street Position', N'statestreet/IN/PRD', N'GIL_BO_GLA105_.*\.csv', N'Regex', 3, CAST(N'08:00' AS time(0)), 480, N'1,2,3,4,5'),
    (N'SST GLB104', N'State Street Position', N'statestreet/IN/PRD', N'GIL_BO_GLB104_.*\.csv', N'Regex', 3, CAST(N'08:00' AS time(0)), 480, N'1,2,3,4,5'),
    (N'SST SLS301', N'State Street Position', N'statestreet/IN/PRD', N'GIL_BO_SLS301_.*\.csv', N'Regex', 3, CAST(N'08:00' AS time(0)), 480, N'1,2,3,4,5'),
    (N'SST ZSC102', N'State Street Position', N'statestreet/IN/PRD', N'GIL_BO_ZSC102_.*\.csv', N'Regex', 3, CAST(N'08:00' AS time(0)), 480, N'1,2,3,4,5'),
    (N'SST FXA102 GIL', N'State Street Position', N'statestreet/IN/PRD', N'GIL_BO_FXA102_GIL.*\.csv', N'Regex', 1, CAST(N'14:00' AS time(0)), 120, N'1,2,3,4,5'),
    (N'SST TXN105 GIL', N'State Street Position', N'statestreet/IN/PRD', N'GIL_BO_TXN105_GIL.*\.csv', N'Regex', 1, CAST(N'14:00' AS time(0)), 120, N'1,2,3,4,5'),
    (N'SST IAPOS GIL', N'State Street Position', N'statestreet/IN/PRD', N'GIL_BO_IAPOS_GIL.*\.csv', N'Regex', 1, CAST(N'14:00' AS time(0)), 120, N'1,2,3,4,5'),
    (N'SST IAPOSC', N'State Street Position', N'statestreet/IN/PRD', N'.*IAPOSC.*\.CSV', N'Regex', 3, CAST(N'12:00' AS time(0)), 300, N'1,2,3,4,5'),
    (N'SST NVS', N'State Street NAV', N'statestreet/IN/PRD', N'GIL_BO_NVS_.*\.csv', N'Regex', 4, CAST(N'14:00' AS time(0)), 180, N'1,2,3,4,5'),
    (N'SST CUS102', N'State Street Custody', N'statestreet/IN/PRD', N'GIL_CUS102_\d{8}\.txt', N'Regex', 1, CAST(N'04:00' AS time(0)), 120, N'1,2,3,4,5'),
    (N'SST Opening Cash Balances', N'State Street Custody', N'statestreet/IN/PRD', N'Opening_Cash_Balances_Generali_\d{8}\.csv', N'Regex', 1, CAST(N'06:00' AS time(0)), 60, N'1,2,3,4,5'),
    (N'SST GLA Daily T0', N'State Street TA', N'statestreet/IN/PRD', N'GLA_.*Daily_T0_Funds.*', N'Regex', 2, CAST(N'16:00' AS time(0)), 60, N'1,2,3,4,5'),
    (N'SST GLI Daily T0', N'State Street TA', N'statestreet/IN/PRD', N'GLI_.*Daily_T0_Funds.*', N'Regex', 2, CAST(N'16:00' AS time(0)), 60, N'1,2,3,4,5'),
    (N'SST CRLQRPT', N'State Street TA', N'statestreet/IN/PRD', N'.*_CRLQRPT.*\.csv', N'Regex', 2, CAST(N'16:00' AS time(0)), 60, N'1,2,3,4,5'),
    (N'SST Clients Data', N'State Street Clients', N'statestreet/IN/PRD', N'.*_ST_\d{8}_CLIENTS_.*\.txt', N'Regex', 24, CAST(N'23:00' AS time(0)), 59, N'1,2,3,4,5'),
    (N'SST APLX Reports', N'State Street SecLending', N'statestreet/IN/PRD', N'APLX.*\.XLSX', N'Regex', 3, CAST(N'13:00' AS time(0)), 60, N'1,2,3,4,5'),
    (N'SST RGLAD Reports', N'State Street Reports', N'statestreet/IN/PRD', N'RGLAD1-.*\.csv', N'Regex', 3, CAST(N'15:00' AS time(0)), 60, N'1,2,3,4,5'),
    (N'SST RGLID Reports', N'State Street Reports', N'statestreet/IN/PRD', N'RGLID1-.*\.csv', N'Regex', 3, CAST(N'15:00' AS time(0)), 60, N'1,2,3,4,5'),
    (N'SST PF Aperture', N'State Street Performance', N'statestreet/IN/PRD', N'PF_Aperture_.*\.xlsx', N'Regex', 1, CAST(N'12:00' AS time(0)), 60, N'1,2,3,4,5'),
    (N'SST PF Plenisfer', N'State Street Performance', N'statestreet/IN/PRD', N'PF_Plenisfer_.*\.xlsx', N'Regex', 1, CAST(N'12:00' AS time(0)), 60, N'1,2,3,4,5'),
    (N'SST PF Fulcrum Fee', N'State Street Performance', N'statestreet/IN/PRD', N'PF_GN.* Fulcrum fee.*\.xlsb', N'Regex', 3, CAST(N'12:00' AS time(0)), 60, N'1,2,3,4,5'),
    (N'SST PF Perf Fee', N'State Street Performance', N'statestreet/IN/PRD', N'PF_Perf fee GN.*\.xlsx', N'Regex', 3, CAST(N'12:00' AS time(0)), 60, N'1,2,3,4,5')
) AS source (Name, Provider, RelativePath, FileNamePattern, MatchType, ExpectedFileCount, ExpectedDeliveryTime, BufferMinutes, DaysOfWeek)
    ON target.Name = source.Name
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Name, Provider, RelativePath, FileNamePattern, MatchType, ExpectedFileCount, Frequency,
            ExpectedDeliveryTime, BufferMinutes, TimeZoneId, FilenameDateMode, IsEnabled, DaysOfWeek)
    VALUES (source.Name, source.Provider, source.RelativePath, source.FileNamePattern, source.MatchType,
            source.ExpectedFileCount, N'Daily', source.ExpectedDeliveryTime, source.BufferMinutes,
            N'Europe/Luxembourg', N'NoDate', 0, source.DaysOfWeek);
GO

-- Result: expect 63 rows (the test definition plus 62 flows), and every flow disabled.
SELECT COUNT(*) AS Definitions, SUM(CASE WHEN IsEnabled = 0 THEN 1 ELSE 0 END) AS Disabled
FROM FileScan.MonitoringDefinition;
GO
