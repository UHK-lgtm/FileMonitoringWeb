namespace FileMonitoringWeb.Server.Models;

/// One row of FileScan.MonitoringDefinition, as returned by GET /api/monitoring-definitions.
public sealed record MonitoringDefinition(
    int Id,
    string Name,
    string RelativePath,
    string FileNamePattern,
    bool IsEnabled);
