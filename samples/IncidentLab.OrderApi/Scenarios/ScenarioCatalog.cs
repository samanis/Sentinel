namespace IncidentLab.OrderApi.Scenarios;

public static class ScenarioCatalog
{
    private static readonly IReadOnlyDictionary<string, ScenarioDefinition> Definitions =
        new Dictionary<string, ScenarioDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["slow-database"] = new(
                "slow-database",
                "Adds database latency while requests continue to succeed.",
                StatusCodes.Status200OK,
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
        "unhandled-exception" => ScenarioKind.UnhandledException,
        _ => ScenarioKind.None
    };
}
