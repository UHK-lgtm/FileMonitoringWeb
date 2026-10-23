namespace FileMonitoringWeb.Server.Models;

/// One row of the deliveries list (docs/api-contract.md, GET /api/deliveries).
public sealed record DeliveryListItemDto(
    long ExpectedDeliveryId,
    int MonitoringDefinitionId,
    string DefinitionName,
    DateOnly ExpectedDate,
    string Status,
    int ExpectedFileCount,
    int ReceivedFileCount,
    int UnexpectedFileCount,
    DateTime DeadlineUtc,
    DateTime? CompletedAtUtc,
    int WarningCount);

public sealed record DeliveryFactsDto(
    long ExpectedDeliveryId,
    int MonitoringDefinitionId,
    string DefinitionName,
    DateOnly ExpectedDate,
    string RelativePath,
    string FileNamePattern,
    string TimeZoneId,
    string Status,
    int ExpectedFileCount,
    int ReceivedFileCount,
    int UnexpectedFileCount,
    DateTime DeadlineUtc,
    DateTime? CompletedAtUtc);

public sealed record TransitionDto(
    string? FromStatus,
    string ToStatus,
    DateTime OccurredAtUtc,
    DateTime RecordedAtUtc,
    int ExpectedFileCount,
    int ReceivedFileCount,
    DateTime DeadlineUtc);

public sealed record FileObservationDto(
    string FileName,
    string RelativePath,
    long SizeBytes,
    DateTime? FileCreatedUtc,
    DateTime? FileLastModifiedUtc,
    DateTime ObservedAtUtc,
    bool IsUnexpected);

public sealed record WarningDto(
    string WarningType,
    string FileName,
    string? Detail,
    DateTime DetectedAtUtc,
    DateTime? ResolvedAtUtc);

public sealed record CommentDto(
    long CommentId,
    string CommentText,
    string CreatedBy,
    DateTime CreatedAtUtc);

public sealed record DeliveryDetailDto(
    DeliveryFactsDto Delivery,
    IReadOnlyList<TransitionDto> Transitions,
    IReadOnlyList<FileObservationDto> Files,
    IReadOnlyList<WarningDto> Warnings,
    IReadOnlyList<CommentDto> Comments);

/// The body of POST /api/deliveries/{id}/comments.
public sealed record CommentInput(string? Text);
