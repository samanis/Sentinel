using IncidentLab.TelemetryGenerator;

namespace IncidentLab.TelemetryGenerator.Tests;

public sealed class GeneratorStateTests
{
    [Fact]
    public void AutomatedFailureDefaultsIncludeAResolutionWindow()
    {
        var options = new TelemetryGeneratorOptions();

        Assert.True(options.AutomatedFailuresEnabled);
        Assert.Contains("slow-database", options.FailureScenarioIds);
        Assert.Contains("memory-leak", options.FailureScenarioIds);
        Assert.Equal(45, options.FailureDurationSeconds);
        Assert.True(options.HealthyDurationSeconds >= 75);
    }

    [Fact]
    public void RecordsSuccessAndFailureOutcomes()
    {
        var state = new GeneratorState();
        var observedAt = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

        state.MarkRunning();
        state.RecordSuccess(1, 200, observedAt);
        state.RecordFailure(2, 504, observedAt.AddSeconds(1), "HTTP 504");

        var snapshot = state.GetSnapshot();
        Assert.True(snapshot.IsRunning);
        Assert.Equal(2, snapshot.RequestCount);
        Assert.Equal(1, snapshot.FailureCount);
        Assert.Equal(2, snapshot.LastOrderId);
        Assert.Equal(504, snapshot.LastStatusCode);
        Assert.Equal("HTTP 504", snapshot.LastError);
    }
}
