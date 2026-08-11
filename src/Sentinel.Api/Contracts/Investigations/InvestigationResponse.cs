using Sentinel.Application.Investigations;
using Sentinel.Domain.Investigations;

namespace Sentinel.Api.Contracts.Investigations;

public sealed record InvestigationResponse(
    Guid Id,
    Guid IncidentId,
    InvestigationRunStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? Model,
    string? PromptVersion,
    int TotalEvidenceCount,
    int ConsideredEvidenceCount,
    string? FailureReason,
    IReadOnlyList<RcaEvidenceRelationshipResponse> Relationships,
    IReadOnlyList<RcaHypothesisResponse> Hypotheses)
{
    public static InvestigationResponse From(PersistedInvestigation investigation) => new(
        investigation.Run.Id.Value,
        investigation.Run.IncidentId.Value,
        investigation.Run.Status,
        investigation.Run.StartedAt,
        investigation.Run.CompletedAt,
        investigation.Run.Model,
        investigation.Run.PromptVersion,
        investigation.Run.TotalEvidenceCount,
        investigation.Run.ConsideredEvidenceCount,
        investigation.Run.FailureReason,
        investigation.Relationships.Select(RcaEvidenceRelationshipResponse.From).ToArray(),
        investigation.Hypotheses.Select(RcaHypothesisResponse.From).ToArray());
}
