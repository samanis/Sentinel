using Sentinel.Api.Contracts.Incidents;
using Sentinel.Application.Incidents.CreateIncident;
using Sentinel.Application.Incidents.GetIncident;
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
            .Produces<IncidentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetIncidentAsync)
            .WithName("GetIncident")
            .Produces<IncidentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

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
}
