using Sentinel.Api.Contracts.Ingestion;
using Sentinel.Application.Ingestion;
using Sentinel.Domain.Ingestion;

namespace Sentinel.Api.Endpoints;

public static class IngestionEndpoints
{
    public static IEndpointRouteBuilder MapIngestionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v2/alerts", AcceptPrometheusAlertsAsync)
            .WithTags("Ingestion")
            .WithName("AcceptPrometheusAlerts")
            .WithSummary("Accept a Prometheus alert batch")
            .WithDescription("Durably stores Prometheus alerts and creates pending ingestion runs before acknowledging the request.")
            .Produces<AcceptPrometheusAlertsResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        endpoints.MapPost("/api/alerts/webhook", AcceptAlertmanagerWebhookAsync)
            .WithTags("Ingestion")
            .WithName("AcceptAlertmanagerWebhook")
            .WithSummary("Accept an Alertmanager webhook notification")
            .WithDescription("Durably stores the alerts in an Alertmanager webhook envelope before acknowledging the notification.")
            .Produces<AcceptPrometheusAlertsResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        endpoints.MapGet("/api/ingestion/runs/{id:guid}", GetIngestionRunAsync)
            .WithTags("Ingestion")
            .WithName("GetIngestionRun")
            .WithSummary("Get an ingestion run")
            .Produces<IngestionRunResponse>()
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> AcceptPrometheusAlertsAsync(
        IReadOnlyList<PrometheusAlertHttpRequest> request,
        AcceptPrometheusAlertsUseCase useCase,
        CancellationToken cancellationToken)
    {
        return await AcceptAsync(request, useCase, cancellationToken);
    }

    private static async Task<IResult> AcceptAlertmanagerWebhookAsync(
        AlertmanagerWebhookHttpRequest request,
        AcceptPrometheusAlertsUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (request.Alerts is null)
            throw new ArgumentException("The Alertmanager alerts collection is required.", nameof(request));
        return await AcceptAsync(request.Alerts, useCase, cancellationToken);
    }

    private static async Task<IResult> AcceptAsync(
        IReadOnlyList<PrometheusAlertHttpRequest> alerts,
        AcceptPrometheusAlertsUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            alerts.Select(item => new PrometheusAlertInput(
                item.Labels,
                item.Annotations,
                item.StartsAt,
                item.EndsAt,
                item.GeneratorUrl)).ToArray(),
            cancellationToken);

        return Results.Accepted(value: AcceptPrometheusAlertsResponse.From(result));
    }

    private static async Task<IResult> GetIngestionRunAsync(
        Guid id,
        GetIngestionRunUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("The ingestion run ID cannot be empty.", nameof(id));
        var ingestion = await useCase.ExecuteAsync(new IngestionRunId(id), cancellationToken);
        return ingestion is null ? Results.NotFound() : Results.Ok(IngestionRunResponse.From(ingestion));
    }

}
