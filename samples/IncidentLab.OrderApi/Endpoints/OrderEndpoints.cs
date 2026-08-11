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
        ControlledMemoryLeak memoryLeak,
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
                ScenarioKind.SlowDatabase => await ExecuteFailureAsync(
                    id, scenario, logger, telemetry, activity, SlowDatabaseFailure, cancellationToken),
                ScenarioKind.DatabaseUnavailable => await ExecuteFailureAsync(
                    id, scenario, logger, telemetry, activity, DatabaseUnavailableFailure, cancellationToken),
                ScenarioKind.DependencyTimeout => await ExecuteFailureAsync(
                    id, scenario, logger, telemetry, activity, DatabaseTimeoutFailure, cancellationToken),
                ScenarioKind.ExternalApiTimeout => await ExecuteFailureAsync(
                    id, scenario, logger, telemetry, activity, ExternalApiFailure, cancellationToken),
                ScenarioKind.WebServiceUnavailable => await ExecuteFailureAsync(
                    id, scenario, logger, telemetry, activity, WebServiceFailure, cancellationToken),
                ScenarioKind.FtpTransferFailure => await ExecuteFailureAsync(
                    id, scenario, logger, telemetry, activity, FtpFailure, cancellationToken),
                ScenarioKind.MemoryLeak => await ExecuteFailureAsync(
                    id, scenario, logger, telemetry, activity,
                    MemoryLeakFailure(memoryLeak.Retain()), cancellationToken),
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

    private static async Task<IResult> ExecuteFailureAsync(
        long id,
        ScenarioSnapshot scenario,
        ILogger logger,
        IncidentLabTelemetry telemetry,
        Activity? activity,
        FailureContext context,
        CancellationToken cancellationToken)
    {
        ScenarioFailure(
            logger, id, scenario.Kind, context.DependencyType, context.Operation,
            context.Target, context.SimulatedStatement,
            scenario.DelayMilliseconds, context.StatusCode);
        AddFailureTraceContext(activity, scenario.Kind, context);
        await Task.Delay(scenario.DelayMilliseconds, cancellationToken);

        telemetry.RecordFailure(scenario.Kind);
        activity?.SetStatus(ActivityStatusCode.Error, context.Title);

        return Results.Problem(
            title: context.Title,
            detail: $"Incident Lab injected the {scenario.Kind} scenario against {context.Target}.",
            statusCode: context.StatusCode,
            extensions: ProblemExtensions(scenario.Kind));
    }

    private static void AddFailureTraceContext(
        Activity? activity,
        ScenarioKind scenario,
        FailureContext context)
    {
        if (activity is null) return;

        activity.SetTag("error.type", context.ErrorType);
        activity.SetTag("incidentlab.failure.cause", context.DependencyType);
        activity.SetTag("incidentlab.failure.operation", context.Operation);
        activity.SetTag("incidentlab.failure.target", context.Target);
        activity.SetTag("incidentlab.simulated", true);
        activity.SetTag("incidentlab.simulated.statement", context.SimulatedStatement);
        activity.AddEvent(new ActivityEvent(context.EventName));

        switch (scenario)
        {
            case ScenarioKind.SlowDatabase:
            case ScenarioKind.DatabaseUnavailable:
            case ScenarioKind.DependencyTimeout:
                activity.SetTag("db.system.name", "postgresql");
                activity.SetTag("db.namespace", "orders");
                activity.SetTag("db.operation.name", context.Operation);
                activity.SetTag("db.query.summary", context.SimulatedStatement);
                activity.SetTag("server.address", "orders-db");
                break;
            case ScenarioKind.ExternalApiTimeout:
                activity.SetTag("http.request.method", "POST");
                activity.SetTag("server.address", "payments.example.test");
                break;
            case ScenarioKind.WebServiceUnavailable:
                activity.SetTag("rpc.system", "soap");
                activity.SetTag("rpc.method", context.Operation);
                activity.SetTag("server.address", "inventory-soap.example.test");
                break;
            case ScenarioKind.FtpTransferFailure:
                activity.SetTag("network.protocol.name", "ftp");
                activity.SetTag("server.address", "partner-ftp.example.test");
                break;
            case ScenarioKind.MemoryLeak:
                activity.SetTag("process.memory.failure", "retained_heap_growth");
                break;
        }
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

    private static readonly FailureContext SlowDatabaseFailure = new(
        "database", "SELECT", "orders-db/orders", "SELECT id, status, total FROM orders WHERE id = @orderId",
        "database.query.slow", "timeout", StatusCodes.Status504GatewayTimeout,
        "Orders query exceeded its command timeout");

    private static readonly FailureContext DatabaseUnavailableFailure = new(
        "database", "CONNECT", "orders-db:5432", "Open PostgreSQL connection for orders",
        "database.connection.failed", "connection_refused", StatusCodes.Status503ServiceUnavailable,
        "Order database unavailable");

    private static readonly FailureContext DatabaseTimeoutFailure = new(
        "database", "SELECT", "orders-db/orders", "SELECT id, status, total FROM orders WHERE id = @orderId",
        "database.query.timeout", "timeout", StatusCodes.Status504GatewayTimeout,
        "Order database timed out");

    private static readonly FailureContext ExternalApiFailure = new(
        "external-api", "POST /v1/payments/authorize", "https://payments.example.test",
        "Authorize order payment", "external_api.timeout", "timeout",
        StatusCodes.Status504GatewayTimeout, "Payment API timed out");

    private static readonly FailureContext WebServiceFailure = new(
        "web-service", "ReserveInventory", "https://inventory-soap.example.test/InventoryService.svc",
        "SOAP ReserveInventory request", "web_service.unavailable", "bad_gateway",
        StatusCodes.Status502BadGateway, "Inventory web service unavailable");

    private static readonly FailureContext FtpFailure = new(
        "ftp", "STOR", "ftp://partner-ftp.example.test/outbound/orders.csv",
        "Upload outbound orders.csv", "ftp.transfer.failed", "connection_reset",
        StatusCodes.Status502BadGateway, "Partner FTP transfer failed");

    private static FailureContext MemoryLeakFailure(int retainedBytes) => new(
        "memory", "ALLOCATE", "incidentlab-order-api process",
        $"Retained heap grew to {retainedBytes} bytes (controlled cap {ControlledMemoryLeak.MaximumRetainedBytes} bytes)",
        "process.memory.retained", "resource_exhausted",
        StatusCodes.Status503ServiceUnavailable, "Application memory pressure detected");

    private sealed record FailureContext(
        string DependencyType,
        string Operation,
        string Target,
        string SimulatedStatement,
        string EventName,
        string ErrorType,
        int StatusCode,
        string Title);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "Scenario {Scenario} injected a simulated failure for order {OrderId}. Simulated=true Cause={DependencyType} Operation={Operation} Target={Target} SimulatedStatement={SimulatedStatement} DelayMilliseconds={DelayMilliseconds} HTTP={StatusCode}")]
    private static partial void ScenarioFailure(
        ILogger logger,
        long orderId,
        ScenarioKind scenario,
        string dependencyType,
        string operation,
        string target,
        string simulatedStatement,
        int delayMilliseconds,
        int statusCode);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Error,
        Message = "Unhandled exception scenario failed order {OrderId}")]
    private static partial void UnhandledScenarioException(
        ILogger logger,
        Exception exception,
        long orderId);
}
