namespace IncidentLab.OrderApi.Scenarios;

public sealed record StartScenarioRequest(
    int DurationSeconds = 60,
    int? DelayMilliseconds = null);
