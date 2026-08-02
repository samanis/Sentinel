namespace IncidentLab.OrderApi.Scenarios;

public sealed record ScenarioDefinition(
    string Id,
    string Description,
    int ExpectedStatus,
    int DefaultDelayMilliseconds);
