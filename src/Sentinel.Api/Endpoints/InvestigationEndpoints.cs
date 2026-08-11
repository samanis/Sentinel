using Sentinel.Api.Contracts.Investigations;
using Sentinel.Application.Investigations;
using Sentinel.Domain.Investigations;

namespace Sentinel.Api.Endpoints;

public static class InvestigationEndpoints
{
    public static IEndpointRouteBuilder MapInvestigationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/investigations/{id:guid}", GetInvestigationAsync)
            .WithTags("Investigations")
            .WithName("GetInvestigation")
            .WithSummary("Get a durable RCA investigation")
            .Produces<InvestigationResponse>()
            .Produces(StatusCodes.Status404NotFound);
        return endpoints;
    }

    private static async Task<IResult> GetInvestigationAsync(
        Guid id,
        GetInvestigationUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("The investigation ID cannot be empty.", nameof(id));
        var investigation = await useCase.ExecuteAsync(new InvestigationRunId(id), cancellationToken);
        return investigation is null
            ? Results.NotFound()
            : Results.Ok(InvestigationResponse.From(investigation));
    }
}
