namespace IncidentLab.OrderApi.Scenarios;

public sealed record ScenarioSnapshot(
    ScenarioKind Kind,
    int DelayMilliseconds,
    DateTimeOffset? StartedAt,
    DateTimeOffset? ExpiresAt)
{
    public bool IsActive => Kind is not ScenarioKind.None;
}
