using Sentinel.Domain.Incidents;

namespace Sentinel.Domain.Investigations;

public sealed class InvestigationRun
{
    public const int MaxFailureReasonLength = 2_000;

    private InvestigationRun(
        InvestigationRunId id,
        IncidentId incidentId,
        DateTimeOffset startedAt)
    {
        Id = id;
        IncidentId = incidentId;
        StartedAt = startedAt;
        Status = InvestigationRunStatus.Running;
    }

    public InvestigationRunId Id { get; }
    public IncidentId IncidentId { get; }
    public InvestigationRunStatus Status { get; private set; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? Model { get; private set; }
    public string? PromptVersion { get; private set; }
    public int TotalEvidenceCount { get; private set; }
    public int ConsideredEvidenceCount { get; private set; }
    public string? FailureReason { get; private set; }

    public static InvestigationRun Start(IncidentId incidentId, DateTimeOffset startedAt)
    {
        if (startedAt == default)
            throw new ArgumentException("The investigation start time is required.", nameof(startedAt));
        return new InvestigationRun(InvestigationRunId.New(), incidentId, startedAt);
    }

    public void Complete(
        string model,
        string promptVersion,
        int totalEvidenceCount,
        int consideredEvidenceCount,
        DateTimeOffset completedAt)
    {
        EnsureRunning(completedAt);
        if (string.IsNullOrWhiteSpace(model)) throw new ArgumentException("A model is required.", nameof(model));
        if (string.IsNullOrWhiteSpace(promptVersion)) throw new ArgumentException("A prompt version is required.", nameof(promptVersion));
        ValidateEvidenceCounts(totalEvidenceCount, consideredEvidenceCount);
        Model = model.Trim();
        PromptVersion = promptVersion.Trim();
        TotalEvidenceCount = totalEvidenceCount;
        ConsideredEvidenceCount = consideredEvidenceCount;
        CompletedAt = completedAt;
        Status = InvestigationRunStatus.Completed;
    }

    public void Fail(
        string failureReason,
        int totalEvidenceCount,
        int consideredEvidenceCount,
        DateTimeOffset failedAt)
    {
        EnsureRunning(failedAt);
        if (string.IsNullOrWhiteSpace(failureReason))
            throw new ArgumentException("A failure reason is required.", nameof(failureReason));
        ValidateEvidenceCounts(totalEvidenceCount, consideredEvidenceCount);
        FailureReason = failureReason.Trim().Length <= MaxFailureReasonLength
            ? failureReason.Trim()
            : failureReason.Trim()[..MaxFailureReasonLength];
        TotalEvidenceCount = totalEvidenceCount;
        ConsideredEvidenceCount = consideredEvidenceCount;
        CompletedAt = failedAt;
        Status = InvestigationRunStatus.Failed;
    }

    private void EnsureRunning(DateTimeOffset completedAt)
    {
        if (Status != InvestigationRunStatus.Running)
            throw new InvestigationDomainException("Only a running investigation can be completed.");
        if (completedAt < StartedAt)
            throw new InvestigationDomainException("An investigation cannot complete before it started.");
    }

    private static void ValidateEvidenceCounts(int total, int considered)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(total);
        ArgumentOutOfRangeException.ThrowIfNegative(considered);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(considered, total);
    }
}
