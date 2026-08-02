using IncidentLab.OrderApi.Scenarios;

namespace IncidentLab.OrderApi.Endpoints;

public static class ScenarioEndpoints
{
    public static IEndpointRouteBuilder MapScenarioEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var scenarios = endpoints.MapGroup("/scenarios").WithTags("Incident scenarios");

        scenarios.MapGet("/", () => Results.Ok(ScenarioCatalog.All));
        scenarios.MapGet("/status", (ScenarioEngine engine, TimeProvider timeProvider) =>
            Results.Ok(engine.GetSnapshot(timeProvider.GetUtcNow())));
        scenarios.MapPost("/{scenarioId}/start", StartScenario);
        scenarios.MapPost("/stop", (ScenarioEngine engine) => Results.Ok(engine.Stop()));

        return endpoints;
    }

    private static IResult StartScenario(
        string scenarioId,
        StartScenarioRequest request,
        ScenarioEngine engine,
        TimeProvider timeProvider)
    {
        if (!ScenarioCatalog.TryGet(scenarioId, out var definition) || definition is null)
        {
            return Results.NotFound(new { message = $"Unknown scenario '{scenarioId}'." });
        }

        var delay = request.DelayMilliseconds ?? definition.DefaultDelayMilliseconds;
        var errors = Validate(request.DurationSeconds, delay);

        return errors.Count > 0
            ? Results.ValidationProblem(errors)
            : Results.Ok(engine.Start(
                ScenarioCatalog.GetKind(definition.Id),
                delay,
                request.DurationSeconds,
                timeProvider.GetUtcNow()));
    }

    private static Dictionary<string, string[]> Validate(int durationSeconds, int delayMilliseconds)
    {
        var errors = new Dictionary<string, string[]>();

        if (durationSeconds is < 1 or > ScenarioEngine.MaximumDurationSeconds)
        {
            errors[nameof(durationSeconds)] =
                [$"Duration must be between 1 and {ScenarioEngine.MaximumDurationSeconds} seconds."];
        }

        if (delayMilliseconds is < 0 or > ScenarioEngine.MaximumDelayMilliseconds)
        {
            errors[nameof(delayMilliseconds)] =
                [$"Delay must be between 0 and {ScenarioEngine.MaximumDelayMilliseconds} milliseconds."];
        }

        return errors;
    }
}
