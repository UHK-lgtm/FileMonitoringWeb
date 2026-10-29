using FileMonitoringWeb.Server.Data;
using FileMonitoringWeb.Server.Domain;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace FileMonitoringWeb.Server.DataHealth;

/// Runs every enabled Data Health control on demand and returns the page results. Results are cached briefly so that
/// repeated refreshes do not hit the source tables again. One failing control becomes an Error row and does not stop the others.
public sealed class DataHealthService(
    DataHealthRepository repository,
    IMemoryCache cache,
    ILogger<DataHealthService> logger)
{
    private const string CacheKey = "data-health:results";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);
    private const int MaxParallelChecks = 4;

    public async Task<DataHealthResultsDto> GetResultsAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out DataHealthResultsDto? cached) && cached is not null)
            return cached;

        var results = await ComputeAsync(cancellationToken);
        cache.Set(CacheKey, results, CacheDuration);
        return results;
    }

    private async Task<DataHealthResultsDto> ComputeAsync(CancellationToken cancellationToken)
    {
        var today = LocalDates.Today(LocalDates.Luxembourg);
        var controls = await repository.ListRunnableAsync(cancellationToken);

        using var gate = new SemaphoreSlim(MaxParallelChecks);
        var tasks = controls.Select(async control =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                return await RunControlAsync(control, today, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        });

        var controlResults = await Task.WhenAll(tasks);

        return new DataHealthResultsDto(
            Today: today,
            RefreshedAtUtc: DateTime.UtcNow,
            EnabledSources: controlResults.Length,
            MissingCount: controlResults.Count(result => result.Status == "Missing"),
            DecreaseCount: controlResults.Count(result => result.Status == "Decrease"),
            ErrorCount: controlResults.Count(result => result.Status == "Error"),
            Healthy: controlResults.All(result => !result.Attention),
            Controls: controlResults);
    }

    private async Task<ControlResultDto> RunControlAsync(RunnableControl control, DateOnly today, CancellationToken cancellationToken)
    {
        try
        {
            var controlDate = DataHealthRules.ControlDate(today, control.OffsetDays, control.OffsetType);
            var from = controlDate.AddDays(-control.HistoryDays);
            var toExclusive = today.AddDays(1);

            var counts = await repository.CountByDayAsync(control, from, toExclusive, cancellationToken);

            var controlCount = counts.GetValueOrDefault(controlDate);
            var previousDate = DataHealthRules.PreviousAvailableDate(controlDate, counts);
            int? previousCount = previousDate is { } previous ? counts[previous] : null;
            var status = DataHealthRules.Evaluate(controlCount, previousCount, control.Tolerance);

            DateOnly? latestDate = null;
            int? latestCount = null;
            foreach (var (day, count) in counts.Where(entry => entry.Value > 0).OrderByDescending(entry => entry.Key))
            {
                latestDate = day;
                latestCount = count;
                break;
            }

            var trend = new List<TrendPoint>();
            for (var day = from; day <= today; day = day.AddDays(1))
                trend.Add(new TrendPoint(day, counts.GetValueOrDefault(day)));

            return new ControlResultDto(
                ControlId: control.ControlId,
                Name: control.Name,
                SourceName: control.SourceName,
                ControlDate: controlDate,
                ControlCount: controlCount,
                ComparisonDate: previousDate,
                PreviousCount: previousCount,
                PercentChange: previousCount is { } prior ? DataHealthRules.PercentChange(controlCount, prior) : null,
                LatestVisibleDate: latestDate,
                LatestVisibleCount: latestCount,
                Status: status.ToString(),
                Attention: DataHealthRules.IsAttention(status, control.MissingCheck, control.DecreaseCheck),
                Message: null,
                Trend: trend);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Only the exception type is logged. SQL messages can contain server and object names.
            logger.LogError("Data Health check {ControlId} could not run: {ExceptionType}", control.ControlId, exception.GetType().Name);

            return new ControlResultDto(
                ControlId: control.ControlId,
                Name: control.Name,
                SourceName: control.SourceName,
                ControlDate: today,
                ControlCount: 0,
                ComparisonDate: null,
                PreviousCount: null,
                PercentChange: null,
                LatestVisibleDate: null,
                LatestVisibleCount: null,
                Status: "Error",
                Attention: true,
                Message: "This check could not run. Ask an administrator to look at the logs.",
                Trend: Array.Empty<TrendPoint>());
        }
    }
}
