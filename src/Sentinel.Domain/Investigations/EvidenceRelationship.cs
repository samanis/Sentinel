using Sentinel.Domain.Evidence;
using Sentinel.Domain.Incidents;

namespace Sentinel.Domain.Investigations;

public sealed class EvidenceRelationship
{
    public const int MaxExplanationLength = 4_000;
    public const int MaxModelLength = 200;
    public const int MaxPromptVersionLength = 100;

    private EvidenceRelationship(
        EvidenceRelationshipId id,
        InvestigationRunId investigationRunId,
        IncidentId incidentId,
        EvidenceId sourceEvidenceId,
        EvidenceId targetEvidenceId,
        RelationshipType type,
        CorrelationStrength strength,
        string explanation,
        string model,
        string promptVersion,
        DateTimeOffset createdAt)
    {
        Id = id;
        InvestigationRunId = investigationRunId;
        IncidentId = incidentId;
        SourceEvidenceId = sourceEvidenceId;
        TargetEvidenceId = targetEvidenceId;
        Type = type;
        Strength = strength;
        Explanation = explanation;
        Model = model;
        PromptVersion = promptVersion;
        CreatedAt = createdAt;
    }

    public EvidenceRelationshipId Id { get; }
    public InvestigationRunId InvestigationRunId { get; }
    public IncidentId IncidentId { get; }
    public EvidenceId SourceEvidenceId { get; }
    public EvidenceId TargetEvidenceId { get; }
    public RelationshipType Type { get; }
    public CorrelationStrength Strength { get; }
    public string Explanation { get; }
    public string Model { get; }
    public string PromptVersion { get; }
    public DateTimeOffset CreatedAt { get; }

    public static EvidenceRelationship Create(
        InvestigationRunId investigationRunId,
        IncidentId incidentId,
        EvidenceId sourceEvidenceId,
        EvidenceId targetEvidenceId,
        RelationshipType type,
        CorrelationStrength strength,
        string explanation,
        string model,
        string promptVersion,
        DateTimeOffset createdAt)
    {
        if (sourceEvidenceId == targetEvidenceId)
            throw new ArgumentException("An Evidence item cannot relate to itself.", nameof(targetEvidenceId));
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        if (!Enum.IsDefined(strength)) throw new ArgumentOutOfRangeException(nameof(strength));
        if (createdAt == default)
            throw new ArgumentException("The relationship creation time is required.", nameof(createdAt));

        return new EvidenceRelationship(
            EvidenceRelationshipId.New(), investigationRunId, incidentId, sourceEvidenceId, targetEvidenceId,
            type, strength,
            NormalizeRequired(explanation, MaxExplanationLength, nameof(explanation)),
            NormalizeRequired(model, MaxModelLength, nameof(model)),
            NormalizeRequired(promptVersion, MaxPromptVersionLength, nameof(promptVersion)),
            createdAt);
    }

    private static string NormalizeRequired(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", parameterName);
        var normalized = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length > maxLength)
            throw new ArgumentException($"The value cannot exceed {maxLength} characters.", parameterName);
        return normalized;
    }
}
