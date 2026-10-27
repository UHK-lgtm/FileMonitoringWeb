namespace FileMonitoringWeb.Server.Domain;

/// What the web app shows for the latest scan run.
public enum ScanDisplayState
{
    NoRunRecorded,
    Current,
    Stale,
    Running,
    DidNotFinish,
    Failed,
    Unknown,
}

/// The columns of the latest FileScan.ScanRun row that the display rules need.
public sealed record ScanRunRecord(string Status, DateTime StartedUtc, DateTime? FinishedUtc);

/// Display rules for the latest scan run. Pure, so they are unit tested.
/// Mirrored in FileScan/FileScannerFunctionApp/ScanRunRules.cs. Keep both in step.
public static class ScanStatusRules
{
    /// A successful run this recent counts as current.
    public static readonly TimeSpan CurrentWindow = TimeSpan.FromMinutes(15);

    /// A run still marked Running after this long is treated as one that did not finish.
    public static readonly TimeSpan StuckRunWindow = TimeSpan.FromMinutes(30);

    public static ScanDisplayState Evaluate(ScanRunRecord? latest, DateTime nowUtc)
    {
        if (latest is null)
            return ScanDisplayState.NoRunRecorded;

        // A start time in the future means the clock or the row is wrong. Do not guess.
        if (latest.StartedUtc > nowUtc)
            return ScanDisplayState.Unknown;

        return latest.Status switch
        {
            "Running" => nowUtc - latest.StartedUtc > StuckRunWindow ? ScanDisplayState.DidNotFinish : ScanDisplayState.Running,
            "Failed" => ScanDisplayState.Failed,
            "Succeeded" => nowUtc - (latest.FinishedUtc ?? latest.StartedUtc) <= CurrentWindow ? ScanDisplayState.Current : ScanDisplayState.Stale,
            _ => ScanDisplayState.Unknown,
        };
    }
}
