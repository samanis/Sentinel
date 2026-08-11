using Sentinel.Domain.Investigations;

namespace Sentinel.Application.Investigations.Analysis;

public sealed record ValidatedInvestigationAnalysis(
    string Model,
    string PromptVersion,
    IReadOnlyList<EvidenceRelationship> Relationships,
    IReadOnlyList<Hypothesis> Hypotheses);
