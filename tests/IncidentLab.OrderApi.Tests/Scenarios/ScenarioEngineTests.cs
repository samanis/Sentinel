using IncidentLab.OrderApi.Scenarios;

namespace IncidentLab.OrderApi.Tests.Scenarios;

public sealed class ScenarioEngineTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NewEngineIsInactive()
    {
        var snapshot = new ScenarioEngine().GetSnapshot(Now);

        Assert.False(snapshot.IsActive);
        Assert.Equal(ScenarioKind.None, snapshot.Kind);
    }

    [Theory]
    [InlineData(ScenarioKind.SlowDatabase)]
    [InlineData(ScenarioKind.DatabaseUnavailable)]
    [InlineData(ScenarioKind.DependencyTimeout)]
    [InlineData(ScenarioKind.UnhandledException)]
    public void StartActivatesSupportedScenario(ScenarioKind kind)
    {
        var engine = new ScenarioEngine();

        var snapshot = engine.Start(kind, 500, 60, Now);

        Assert.True(snapshot.IsActive);
        Assert.Equal(kind, snapshot.Kind);
        Assert.Equal(Now.AddSeconds(60), snapshot.ExpiresAt);
    }

    [Fact]
    public void StopDeactivatesScenario()
    {
        var engine = new ScenarioEngine();
        engine.Start(ScenarioKind.SlowDatabase, 500, 60, Now);

        var snapshot = engine.Stop();

        Assert.False(snapshot.IsActive);
    }

    [Fact]
    public void ExpiredScenarioAutomaticallyBecomesInactive()
    {
        var engine = new ScenarioEngine();
        engine.Start(ScenarioKind.DependencyTimeout, 500, 10, Now);

        var snapshot = engine.GetSnapshot(Now.AddSeconds(11));

        Assert.False(snapshot.IsActive);
    }

    [Theory]
    [InlineData(-1, 60)]
    [InlineData(ScenarioEngine.MaximumDelayMilliseconds + 1, 60)]
    [InlineData(500, 0)]
    [InlineData(500, ScenarioEngine.MaximumDurationSeconds + 1)]
    public void StartRejectsInvalidConfiguration(int delayMilliseconds, int durationSeconds)
    {
        var engine = new ScenarioEngine();

        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Start(
            ScenarioKind.SlowDatabase,
            delayMilliseconds,
            durationSeconds,
            Now));
    }
}
