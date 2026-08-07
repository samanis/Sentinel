using Sentinel.Domain.Investigations;

namespace Sentinel.Application.Investigations.Analysis;

public sealed record ProposedInvestigationAnalysis(
    string Model,
    string PromptVersion,
    IReadOnlyList<ProposedEvidenceRelationship> Relationships,
    IReadOnlyList<ProposedHypothesis> Hypotheses);

public sealed record ProposedEvidenceRelationship(
    Guid SourceEvidenceId,
    Guid TargetEvidenceId,
    RelationshipType Type,
    CorrelationStrength Strength,
    string Explanation);

public sealed record ProposedHypothesis(
    HypothesisScope Scope,
    string Statement,
    HypothesisConfidence Confidence,
    string Reasoning,
    IReadOnlyList<ProposedHypothesisEvidenceReference> Evidence);

public sealed record ProposedHypothesisEvidenceReference(
    Guid EvidenceId,
    HypothesisEvidenceRole Role);
