using System.Diagnostics;
using IncidentLab.OrderApi.Orders;
using IncidentLab.OrderApi.Scenarios;
using IncidentLab.OrderApi.Telemetry;

namespace IncidentLab.OrderApi.Endpoints;

public static partial class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/orders/{id:long:min(1)}", GetOrderAsync)
            .WithTags("Orders");

        return endpoints;
    }

    private static async Task<IResult> GetOrderAsync(
        long id,
        ScenarioEngine scenarioEngine,
        TimeProvider timeProvider,
        IncidentLabTelemetry telemetry,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(OrderEndpoints));
        var scenario = scenarioEngine.GetSnapshot(timeProvider.GetUtcNow());
        var stopwatch = Stopwatch.StartNew();

        using var activity = IncidentLabTelemetry.ActivitySource.StartActivity("orders.get");
        activity?.SetTag("order.id", id);
        activity?.SetTag("incidentlab.scenario", scenario.Kind.ToString());
        telemetry.RecordRequest(scenario.Kind);

        try
        {
            return scenario.Kind switch
            {
                ScenarioKind.SlowDatabase => await ExecuteSlowDatabaseAsync(
                    id, scenario, logger, activity, cancellationToken),
                ScenarioKind.DatabaseUnavailable => await ExecuteFailureAsync(
                    id, scenario, logger, telemetry, activity,
                    StatusCodes.Status503ServiceUnavailable,
                    "Order database unavailable",
                    cancellationToken),
                ScenarioKind.DependencyTimeout => await ExecuteFailureAsync(
                    id, scenario, logger, telemetry, activity,
                    StatusCodes.Status504GatewayTimeout,
                    "Order database timed out",
                    cancellationToken),
                ScenarioKind.UnhandledException => ExecuteUnhandledException(
                    id, scenario, logger, telemetry, activity),
                _ => CreateOrder(id)
            };
        }
        finally
        {
            telemetry.RecordDuration(stopwatch.Elapsed.TotalMilliseconds, scenario.Kind);
        }
    }

    private static async Task<IResult> ExecuteSlowDatabaseAsync(
        long id,
        ScenarioSnapshot scenario,
        ILogger logger,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        SlowDatabase(logger, id, scenario.DelayMilliseconds);
        activity?.AddEvent(new ActivityEvent("database.slow"));
        await Task.Delay(scenario.DelayMilliseconds, cancellationToken);
        return CreateOrder(id);
    }

    private static async Task<IResult> ExecuteFailureAsync(
        long id,
        ScenarioSnapshot scenario,
        ILogger logger,
        IncidentLabTelemetry telemetry,
        Activity? activity,
        int statusCode,
        string title,
        CancellationToken cancellationToken)
    {
        ScenarioFailure(logger, id, scenario.Kind, scenario.DelayMilliseconds, statusCode);
        activity?.AddEvent(new ActivityEvent("dependency.failure"));
        await Task.Delay(scenario.DelayMilliseconds, cancellationToken);

        telemetry.RecordFailure(scenario.Kind);
        activity?.SetStatus(ActivityStatusCode.Error, title);

        return Results.Problem(
            title: title,
            detail: $"Incident Lab injected the {scenario.Kind} scenario.",
            statusCode: statusCode,
            extensions: ProblemExtensions(scenario.Kind));
    }

    private static IResult ExecuteUnhandledException(
        long id,
        ScenarioSnapshot scenario,
        ILogger logger,
        IncidentLabTelemetry telemetry,
        Activity? activity)
    {
        try
        {
            throw new InvalidOperationException("Controlled Incident Lab order-processing exception.");
        }
        catch (InvalidOperationException exception)
        {
            UnhandledScenarioException(logger, exception, id);
            telemetry.RecordFailure(scenario.Kind);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddEvent(new ActivityEvent(
                "exception",
                tags: new ActivityTagsCollection
                {
                    ["exception.type"] = exception.GetType().FullName,
                    ["exception.message"] = exception.Message,
                    ["exception.stacktrace"] = exception.StackTrace
                }));

            return Results.Problem(
                title: "Unhandled order-processing exception",
                detail: exception.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                extensions: ProblemExtensions(scenario.Kind));
        }
    }

    private static IResult CreateOrder(long id) => Results.Ok(new OrderResponse(
        id,
        "Processing",
        decimal.Round(25m + ((id % 100) * 1.15m), 2),
        "CAD"));

    private static Dictionary<string, object?> ProblemExtensions(ScenarioKind kind) => new()
    {
        ["scenario"] = kind.ToString(),
        ["traceId"] = Activity.Current?.TraceId.ToString()
    };

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "Scenario {Scenario} failed order {OrderId} after {DelayMilliseconds}ms with HTTP {StatusCode}")]
    private static partial void ScenarioFailure(
        ILogger logger,
        long orderId,
        ScenarioKind scenario,
        int delayMilliseconds,
        int statusCode);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Slow database scenario delayed order {OrderId} by {DelayMilliseconds}ms")]
    private static partial void SlowDatabase(
        ILogger logger,
        long orderId,
        int delayMilliseconds);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Error,
        Message = "Unhandled exception scenario failed order {OrderId}")]
    private static partial void UnhandledScenarioException(
        ILogger logger,
        Exception exception,
        long orderId);
}
