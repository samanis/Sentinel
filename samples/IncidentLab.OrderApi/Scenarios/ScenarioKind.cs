namespace IncidentLab.OrderApi.Scenarios;

public enum ScenarioKind
{
    None,
    SlowDatabase,
    DatabaseUnavailable,
    DependencyTimeout,
    ExternalApiTimeout,
    WebServiceUnavailable,
    FtpTransferFailure,
    MemoryLeak,
    UnhandledException
}
