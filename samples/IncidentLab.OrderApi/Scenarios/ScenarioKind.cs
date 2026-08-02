namespace IncidentLab.OrderApi.Scenarios;

public enum ScenarioKind
{
    None,
    SlowDatabase,
    DatabaseUnavailable,
    DependencyTimeout,
    UnhandledException
}
