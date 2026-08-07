using Sentinel.Application.Investigations.Analysis;
using Sentinel.Domain.Investigations;

namespace Sentinel.Api.Contracts.Investigations;

public sealed record RcaAnalysisResponse(
    Guid InvestigationId,
    Guid IncidentId,
    string Model,
    string PromptVersion,
    int TotalEvidenceCount,
    int ConsideredEvidenceCount,
    IReadOnlyList<RcaEvidenceRelationshipResponse> Relationships,
    IReadOnlyList<RcaHypothesisResponse> Hypotheses)
{
    public static RcaAnalysisResponse From(Guid incidentId, AnalyzeIncidentResult result)
    {
        var analysis = result.Analysis
            ?? throw new ArgumentException("An analyzed result is required.", nameof(result));
        return new(
            result.InvestigationRunId!.Value.Value,
            incidentId,
            analysis.Model,
            analysis.PromptVersion,
            result.TotalEvidenceCount,
            result.ConsideredEvidenceCount,
            analysis.Relationships.Select(RcaEvidenceRelationshipResponse.From).ToArray(),
            analysis.Hypotheses.Select(RcaHypothesisResponse.From).ToArray());
    }
}

public sealed record RcaEvidenceRelationshipResponse(
    Guid Id,
    Guid SourceEvidenceId,
    Guid TargetEvidenceId,
    RelationshipType Type,
    CorrelationStrength Strength,
    string Explanation)
{
    public static RcaEvidenceRelationshipResponse From(EvidenceRelationship relationship) => new(
        relationship.Id.Value,
        relationship.SourceEvidenceId.Value,
        relationship.TargetEvidenceId.Value,
        relationship.Type,
        relationship.Strength,
        relationship.Explanation);
}

public sealed record RcaHypothesisResponse(
    Guid Id,
    HypothesisScope Scope,
    string Statement,
    HypothesisConfidence Confidence,
    string Reasoning,
    IReadOnlyList<RcaHypothesisEvidenceResponse> Evidence)
{
    public static RcaHypothesisResponse From(Hypothesis hypothesis) => new(
        hypothesis.Id.Value,
        hypothesis.Scope,
        hypothesis.Statement,
        hypothesis.Confidence,
        hypothesis.Reasoning,
        hypothesis.EvidenceReferences.Select(reference => new RcaHypothesisEvidenceResponse(
            reference.EvidenceId.Value,
            reference.Role)).ToArray());
}

public sealed record RcaHypothesisEvidenceResponse(
    Guid EvidenceId,
    HypothesisEvidenceRole Role);
