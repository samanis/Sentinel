using Sentinel.Domain.Evidence;
using Sentinel.Domain.Incidents;
using Sentinel.Domain.Investigations;

namespace Sentinel.Application.Investigations.Analysis;

public static class InvestigationAnalysisValidator
{
    public const int MaxHypotheses = 10;
    public const int MaxRelationships = 50;

    public static ValidatedInvestigationAnalysis Validate(
        InvestigationRunId investigationRunId,
        IncidentId incidentId,
        IReadOnlyCollection<EvidenceItem> availableEvidence,
        ProposedInvestigationAnalysis proposal,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(availableEvidence);
        ArgumentNullException.ThrowIfNull(proposal);
        var errors = new List<string>();
        ValidateRequired(proposal.Model, Hypothesis.MaxModelLength, "model", errors);
        ValidateRequired(proposal.PromptVersion, Hypothesis.MaxPromptVersionLength, "promptVersion", errors);
        if (proposal.Hypotheses.Count > MaxHypotheses)
            errors.Add($"At most {MaxHypotheses} hypotheses are allowed.");
        if (proposal.Relationships.Count > MaxRelationships)
            errors.Add($"At most {MaxRelationships} relationships are allowed.");

        var evidenceById = availableEvidence
            .Where(item => item.IncidentId == incidentId)
            .ToDictionary(item => item.Id.Value);
        ValidateRelationships(proposal.Relationships, evidenceById, errors);
        ValidateHypotheses(proposal.Hypotheses, evidenceById, errors);

        if (errors.Count > 0)
            throw new InvestigationAnalysisValidationException(errors);

        var relationships = proposal.Relationships.Select(item => EvidenceRelationship.Create(
            investigationRunId,
            incidentId,
            new EvidenceId(item.SourceEvidenceId),
            new EvidenceId(item.TargetEvidenceId),
            item.Type,
            item.Strength,
            item.Explanation,
            proposal.Model,
            proposal.PromptVersion,
            createdAt)).ToArray();
        var hypotheses = proposal.Hypotheses.Select(item => Hypothesis.Create(
            investigationRunId,
            incidentId,
            item.Scope,
            item.Statement,
            item.Confidence,
            item.Reasoning,
            item.Evidence.Select(reference => new HypothesisEvidenceReference(
                new EvidenceId(reference.EvidenceId), reference.Role)),
            proposal.Model,
            proposal.PromptVersion,
            createdAt)).ToArray();

        return new ValidatedInvestigationAnalysis(
            proposal.Model.Trim(), proposal.PromptVersion.Trim(), relationships, hypotheses);
    }

    private static void ValidateRelationships(
        IReadOnlyList<ProposedEvidenceRelationship> relationships,
        Dictionary<Guid, EvidenceItem> evidence,
        List<string> errors)
    {
        var identities = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < relationships.Count; index++)
        {
            var item = relationships[index];
            var path = $"relationships[{index}]";
            if (!Enum.IsDefined(item.Type)) errors.Add($"{path}.type is invalid.");
            if (!Enum.IsDefined(item.Strength)) errors.Add($"{path}.strength is invalid.");
            ValidateRequired(item.Explanation, EvidenceRelationship.MaxExplanationLength, $"{path}.explanation", errors);
            if (item.SourceEvidenceId == item.TargetEvidenceId)
                errors.Add($"{path} cannot relate Evidence to itself.");

            var sourceExists = evidence.TryGetValue(item.SourceEvidenceId, out var source);
            var targetExists = evidence.TryGetValue(item.TargetEvidenceId, out var target);
            if (!sourceExists) errors.Add($"{path}.sourceEvidenceId does not exist in this incident.");
            if (!targetExists) errors.Add($"{path}.targetEvidenceId does not exist in this incident.");
            if (sourceExists && targetExists && item.Strength == CorrelationStrength.Exact &&
                !SharesExactIdentifier(source!, target!))
                errors.Add($"{path} claims Exact strength without a shared trace or span ID.");

            var first = item.SourceEvidenceId.CompareTo(item.TargetEvidenceId) <= 0
                ? item.SourceEvidenceId : item.TargetEvidenceId;
            var second = first == item.SourceEvidenceId ? item.TargetEvidenceId : item.SourceEvidenceId;
            if (!identities.Add($"{first:D}:{second:D}:{item.Type}"))
                errors.Add($"{path} duplicates another relationship.");
        }
    }

    private static void ValidateHypotheses(
        IReadOnlyList<ProposedHypothesis> hypotheses,
        Dictionary<Guid, EvidenceItem> evidence,
        List<string> errors)
    {
        for (var index = 0; index < hypotheses.Count; index++)
        {
            var item = hypotheses[index];
            var path = $"hypotheses[{index}]";
            if (!Enum.IsDefined(item.Scope)) errors.Add($"{path}.scope is invalid.");
            if (!Enum.IsDefined(item.Confidence)) errors.Add($"{path}.confidence is invalid.");
            ValidateRequired(item.Statement, Hypothesis.MaxStatementLength, $"{path}.statement", errors);
            ValidateRequired(item.Reasoning, Hypothesis.MaxReasoningLength, $"{path}.reasoning", errors);

            var seen = new HashSet<Guid>();
            var hasSupporting = false;
            foreach (var reference in item.Evidence)
            {
                if (!Enum.IsDefined(reference.Role))
                    errors.Add($"{path} contains an invalid Evidence role.");
                if (!seen.Add(reference.EvidenceId))
                    errors.Add($"{path} assigns multiple roles to Evidence {reference.EvidenceId:D}.");
                if (!evidence.TryGetValue(reference.EvidenceId, out var evidenceItem))
                {
                    errors.Add($"{path} references Evidence outside this incident: {reference.EvidenceId:D}.");
                    continue;
                }

                if (reference.Role == HypothesisEvidenceRole.Supporting)
                {
                    hasSupporting = true;
                    if (item.Scope == HypothesisScope.Event &&
                        evidenceItem.SourceTraceId is null && evidenceItem.SourceSpanId is null)
                        errors.Add($"{path} uses non-event Evidence {reference.EvidenceId:D} as Event-scope support.");
                }
            }
            if (!hasSupporting) errors.Add($"{path} must contain supporting Evidence.");
        }
    }

    private static bool SharesExactIdentifier(EvidenceItem first, EvidenceItem second) =>
        (!string.IsNullOrWhiteSpace(first.SourceTraceId) &&
            string.Equals(first.SourceTraceId, second.SourceTraceId, StringComparison.OrdinalIgnoreCase)) ||
        (!string.IsNullOrWhiteSpace(first.SourceSpanId) &&
            string.Equals(first.SourceSpanId, second.SourceSpanId, StringComparison.OrdinalIgnoreCase));

    private static void ValidateRequired(
        string? value, int maxLength, string path, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add($"{path} is required.");
        else if (value.Trim().Length > maxLength) errors.Add($"{path} exceeds {maxLength} characters.");
    }
}
