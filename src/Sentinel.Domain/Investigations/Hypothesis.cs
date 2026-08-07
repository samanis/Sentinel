using Sentinel.Domain.Evidence;
using Sentinel.Domain.Incidents;

namespace Sentinel.Domain.Investigations;

public sealed class Hypothesis
{
    public const int MaxStatementLength = 2_000;
    public const int MaxReasoningLength = 8_000;
    public const int MaxModelLength = 200;
    public const int MaxPromptVersionLength = 100;

    private readonly List<HypothesisEvidenceReference> evidenceReferences = [];

    private Hypothesis()
    {
        Statement = null!;
        Reasoning = null!;
        Model = null!;
        PromptVersion = null!;
    }

    private Hypothesis(
        HypothesisId id,
        InvestigationRunId investigationRunId,
        IncidentId incidentId,
        HypothesisScope scope,
        string statement,
        HypothesisConfidence confidence,
        string reasoning,
        string model,
        string promptVersion,
        DateTimeOffset createdAt,
        List<HypothesisEvidenceReference> evidenceReferences)
    {
        Id = id;
        InvestigationRunId = investigationRunId;
        IncidentId = incidentId;
        Scope = scope;
        Statement = statement;
        Confidence = confidence;
        Reasoning = reasoning;
        Model = model;
        PromptVersion = promptVersion;
        CreatedAt = createdAt;
        this.evidenceReferences = evidenceReferences;
    }

    public HypothesisId Id { get; }
    public InvestigationRunId InvestigationRunId { get; }
    public IncidentId IncidentId { get; }
    public HypothesisScope Scope { get; }
    public string Statement { get; }
    public HypothesisConfidence Confidence { get; }
    public string Reasoning { get; }
    public string Model { get; }
    public string PromptVersion { get; }
    public DateTimeOffset CreatedAt { get; }
    public IReadOnlyList<HypothesisEvidenceReference> EvidenceReferences => evidenceReferences;

    public static Hypothesis Create(
        InvestigationRunId investigationRunId,
        IncidentId incidentId,
        HypothesisScope scope,
        string statement,
        HypothesisConfidence confidence,
        string reasoning,
        IEnumerable<HypothesisEvidenceReference> evidenceReferences,
        string model,
        string promptVersion,
        DateTimeOffset createdAt) =>
        Create(
            HypothesisId.New(), investigationRunId, incidentId, scope, statement, confidence, reasoning,
            evidenceReferences, model, promptVersion, createdAt);

    private static Hypothesis Create(
        HypothesisId id,
        InvestigationRunId investigationRunId,
        IncidentId incidentId,
        HypothesisScope scope,
        string statement,
        HypothesisConfidence confidence,
        string reasoning,
        IEnumerable<HypothesisEvidenceReference> evidenceReferences,
        string model,
        string promptVersion,
        DateTimeOffset createdAt)
    {
        if (!Enum.IsDefined(confidence))
            throw new ArgumentOutOfRangeException(nameof(confidence));
        if (!Enum.IsDefined(scope))
            throw new ArgumentOutOfRangeException(nameof(scope));
        if (createdAt == default)
            throw new ArgumentException("The hypothesis creation time is required.", nameof(createdAt));

        var references = NormalizeReferences(evidenceReferences);
        if (!references.Any(reference => reference.Role == HypothesisEvidenceRole.Supporting))
            throw new ArgumentException("A hypothesis must reference at least one supporting Evidence item.", nameof(evidenceReferences));

        return new Hypothesis(
            id, investigationRunId, incidentId, scope,
            NormalizeRequired(statement, MaxStatementLength, nameof(statement)),
            confidence,
            NormalizeRequired(reasoning, MaxReasoningLength, nameof(reasoning)),
            NormalizeRequired(model, MaxModelLength, nameof(model)),
            NormalizeRequired(promptVersion, MaxPromptVersionLength, nameof(promptVersion)),
            createdAt,
            references);
    }

    private static List<HypothesisEvidenceReference> NormalizeReferences(
        IEnumerable<HypothesisEvidenceReference> references)
    {
        ArgumentNullException.ThrowIfNull(references);
        var result = new List<HypothesisEvidenceReference>();
        foreach (var reference in references)
        {
            if (!Enum.IsDefined(reference.Role))
                throw new ArgumentOutOfRangeException(nameof(references), "Unsupported Evidence role.");
            if (result.Any(existing => existing.EvidenceId == reference.EvidenceId))
                throw new ArgumentException("An Evidence item cannot have multiple roles in one hypothesis.", nameof(references));
            result.Add(reference);
        }
        return result;
    }

    private static string NormalizeRequired(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A value is required.", parameterName);
        var normalized = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length > maxLength)
            throw new ArgumentException($"The value cannot exceed {maxLength} characters.", parameterName);
        return normalized;
    }
}
