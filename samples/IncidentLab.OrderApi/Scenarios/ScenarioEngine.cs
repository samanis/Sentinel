namespace IncidentLab.OrderApi.Scenarios;

public sealed class ScenarioEngine
{
    public const int MaximumDelayMilliseconds = 30_000;
    public const int MaximumDurationSeconds = 3_600;

    private readonly object sync = new();
    private ScenarioSnapshot snapshot = Disabled;

    private static ScenarioSnapshot Disabled => new(ScenarioKind.None, 0, null, null);

    public ScenarioSnapshot GetSnapshot(DateTimeOffset now)
    {
        lock (sync)
        {
            if (snapshot.ExpiresAt <= now)
            {
                snapshot = Disabled;
            }

            return snapshot;
        }
    }

    public ScenarioSnapshot Start(
        ScenarioKind kind,
        int delayMilliseconds,
        int durationSeconds,
        DateTimeOffset startedAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(kind, ScenarioKind.None);
        ArgumentOutOfRangeException.ThrowIfNegative(delayMilliseconds);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(delayMilliseconds, MaximumDelayMilliseconds);
        ArgumentOutOfRangeException.ThrowIfLessThan(durationSeconds, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(durationSeconds, MaximumDurationSeconds);

        lock (sync)
        {
            snapshot = new ScenarioSnapshot(
                kind,
                delayMilliseconds,
                startedAt,
                startedAt.AddSeconds(durationSeconds));
            return snapshot;
        }
    }

    public ScenarioSnapshot Stop()
    {
        lock (sync)
        {
            snapshot = Disabled;
            return snapshot;
        }
    }
}
