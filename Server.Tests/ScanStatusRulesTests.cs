using FileMonitoringWeb.Server.Domain;
using FileMonitoringWeb.Server.Models;

namespace FileMonitoringWeb.Server.Tests;

public class ScanStatusRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 27, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SuccessFiveMinutesAgo_IsCurrent()
    {
        var run = new ScanRunRecord("Succeeded", Now.AddMinutes(-6), Now.AddMinutes(-5));

        Assert.Equal(ScanDisplayState.Current, ScanStatusRules.Evaluate(run, Now));
    }

    [Fact]
    public void SuccessTwentyMinutesAgo_IsStale()
    {
        var run = new ScanRunRecord("Succeeded", Now.AddMinutes(-21), Now.AddMinutes(-20));

        Assert.Equal(ScanDisplayState.Stale, ScanStatusRules.Evaluate(run, Now));
    }

    [Fact]
    public void SuccessExactlyFifteenMinutesAgo_IsCurrent()
    {
        var run = new ScanRunRecord("Succeeded", Now.AddMinutes(-16), Now.AddMinutes(-15));

        Assert.Equal(ScanDisplayState.Current, ScanStatusRules.Evaluate(run, Now));
    }

    [Fact]
    public void NoRunRecorded_IsNoRunRecorded()
    {
        Assert.Equal(ScanDisplayState.NoRunRecorded, ScanStatusRules.Evaluate(null, Now));
    }

    [Fact]
    public void RunningTenMinutes_IsRunning()
    {
        var run = new ScanRunRecord("Running", Now.AddMinutes(-10), null);

        Assert.Equal(ScanDisplayState.Running, ScanStatusRules.Evaluate(run, Now));
    }

    [Fact]
    public void RunningFortyFiveMinutes_DidNotFinish()
    {
        var run = new ScanRunRecord("Running", Now.AddMinutes(-45), null);

        Assert.Equal(ScanDisplayState.DidNotFinish, ScanStatusRules.Evaluate(run, Now));
    }

    [Fact]
    public void LatestFailed_IsFailed()
    {
        var run = new ScanRunRecord("Failed", Now.AddMinutes(-3), Now.AddMinutes(-2));

        Assert.Equal(ScanDisplayState.Failed, ScanStatusRules.Evaluate(run, Now));
    }

    [Fact]
    public void StartedInTheFuture_IsUnknown()
    {
        var run = new ScanRunRecord("Succeeded", Now.AddMinutes(5), Now.AddMinutes(6));

        Assert.Equal(ScanDisplayState.Unknown, ScanStatusRules.Evaluate(run, Now));
    }

    [Fact]
    public void UnknownStatusValue_IsUnknown()
    {
        var run = new ScanRunRecord("Bogus", Now.AddMinutes(-1), null);

        Assert.Equal(ScanDisplayState.Unknown, ScanStatusRules.Evaluate(run, Now));
    }

    [Fact]
    public void LatestScanRunResponse_DoesNotExposeFailureDetails()
    {
        var propertyNames = typeof(LatestScanRunDto).GetProperties().Select(property => property.Name).ToList();

        Assert.DoesNotContain("FailureStage", propertyNames);
        Assert.DoesNotContain("FailureType", propertyNames);
    }
}
