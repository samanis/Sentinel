namespace IncidentLab.OrderApi.Scenarios;

public static class ScenarioCatalog
{
    private static readonly IReadOnlyDictionary<string, ScenarioDefinition> Definitions =
        new Dictionary<string, ScenarioDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["slow-database"] = new(
                "slow-database",
                "Simulates an orders query exceeding its database command timeout.",
                StatusCodes.Status504GatewayTimeout,
                1_500),
            ["database-unavailable"] = new(
                "database-unavailable",
                "Simulates an unavailable orders database.",
                StatusCodes.Status503ServiceUnavailable,
                500),
            ["dependency-timeout"] = new(
                "dependency-timeout",
                "Simulates a downstream database command timeout.",
                StatusCodes.Status504GatewayTimeout,
                2_000),
            ["external-api-timeout"] = new(
                "external-api-timeout",
                "Simulates a timeout calling an external payment REST API.",
                StatusCodes.Status504GatewayTimeout,
                1_000),
            ["web-service-unavailable"] = new(
                "web-service-unavailable",
                "Simulates an unavailable legacy SOAP inventory web service.",
                StatusCodes.Status502BadGateway,
                500),
            ["ftp-transfer-failure"] = new(
                "ftp-transfer-failure",
                "Simulates an FTP partner file-transfer failure.",
                StatusCodes.Status502BadGateway,
                750),
            ["memory-leak"] = new(
                "memory-leak",
                "Simulates bounded retained-memory growth and resource exhaustion.",
                StatusCodes.Status503ServiceUnavailable,
                100),
            ["unhandled-exception"] = new(
                "unhandled-exception",
                "Produces a controlled application exception and HTTP 500 response.",
                StatusCodes.Status500InternalServerError,
                0)
        };

    public static IReadOnlyCollection<ScenarioDefinition> All { get; } = [.. Definitions.Values];

    public static bool TryGet(string id, out ScenarioDefinition? definition) =>
        Definitions.TryGetValue(id, out definition);

    public static ScenarioKind GetKind(string id) => id.ToLowerInvariant() switch
    {
        "slow-database" => ScenarioKind.SlowDatabase,
        "database-unavailable" => ScenarioKind.DatabaseUnavailable,
        "dependency-timeout" => ScenarioKind.DependencyTimeout,
        "external-api-timeout" => ScenarioKind.ExternalApiTimeout,
        "web-service-unavailable" => ScenarioKind.WebServiceUnavailable,
        "ftp-transfer-failure" => ScenarioKind.FtpTransferFailure,
        "memory-leak" => ScenarioKind.MemoryLeak,
        "unhandled-exception" => ScenarioKind.UnhandledException,
        _ => ScenarioKind.None
    };
}
