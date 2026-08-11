using Sentinel.Api.Contracts.Incidents;
using Sentinel.Api.Contracts.Investigations;
using Sentinel.Application.Incidents.CreateIncident;
using Sentinel.Application.Incidents.GetIncident;
using Sentinel.Application.Investigations.Analysis;
using Sentinel.Domain.Incidents;

namespace Sentinel.Api.Endpoints;

public static class IncidentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/incidents")
            .WithTags("Incidents");

        group.MapPost("/", CreateIncidentAsync)
            .WithName("CreateIncident")
            .WithSummary("Create an incident")
            .WithDescription("Creates the durable incident context used by later investigations and evidence collection.")
            .Produces<IncidentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetIncidentAsync)
            .WithName("GetIncident")
            .WithSummary("Get an incident")
            .WithDescription("Returns an incident by its unique identifier.")
            .Produces<IncidentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/investigations", AnalyzeIncidentAsync)
            .WithName("AnalyzeIncidentRootCause")
            .WithSummary("Run the bounded RCA agent")
            .WithDescription("Runs bounded RCA and atomically persists the completed investigation and immutable hypotheses.")
            .Produces<RcaAnalysisResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return endpoints;
    }

    private static async Task<IResult> CreateIncidentAsync(
        CreateIncidentHttpRequest request,
        CreateIncidentUseCase useCase,
        CancellationToken cancellationToken)
    {
        var details = await useCase.ExecuteAsync(
            new CreateIncidentRequest(
                request.Title,
                request.Service,
                request.StartedAt,
                request.Severity),
            cancellationToken);
        var response = IncidentResponse.From(details);

        return Results.CreatedAtRoute("GetIncident", new { id = response.Id }, response);
    }

    private static async Task<IResult> GetIncidentAsync(
        Guid id,
        GetIncidentUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The incident ID cannot be empty.", nameof(id));
        }

        var details = await useCase.ExecuteAsync(
            new GetIncidentRequest(new IncidentId(id)),
            cancellationToken);

        return details is null
            ? Results.NotFound()
            : Results.Ok(IncidentResponse.From(details));
    }

    private static async Task<IResult> AnalyzeIncidentAsync(
        Guid id,
        AnalyzeIncidentUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("The incident ID cannot be empty.", nameof(id));

        var result = await useCase.ExecuteAsync(new IncidentId(id), cancellationToken);
        return result.Status switch
        {
            AnalyzeIncidentStatus.IncidentNotFound => Results.NotFound(),
            AnalyzeIncidentStatus.NoEvidence => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "No evidence available",
                detail: $"Import or add Evidence before retrying. Failed investigation: {result.InvestigationRunId!.Value.Value:D}"),
            AnalyzeIncidentStatus.Analyzed => Results.Ok(RcaAnalysisResponse.From(id, result)),
            _ => throw new InvalidOperationException("Unsupported RCA analysis status.")
        };
    }
}
