namespace FileMonitoringWeb.Server.Models;

/// The latest production scan run, from FileScan.ScanRun. The failure stage and failure type are deliberately not included:
/// they are for the logs and the operators, not for every signed-in user.
public sealed record LatestScanRunDto(
    long ScanRunId,
    DateTime StartedUtc,
    DateTime? FinishedUtc,
    string Status,
    int? ScheduledToday,
    int? OpenDeliveries,
    int? StatusChanges);
