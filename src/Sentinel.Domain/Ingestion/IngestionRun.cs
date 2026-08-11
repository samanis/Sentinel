namespace Sentinel.Domain.Ingestion;

public sealed class IngestionRun
{
    private IngestionRun(
        IngestionRunId id,
        AlertOccurrenceId alertOccurrenceId,
        DateTimeOffset createdAt)
    {
        Id = id;
        AlertOccurrenceId = alertOccurrenceId;
        Status = IngestionRunStatus.Pending;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public IngestionRunId Id { get; }
    public AlertOccurrenceId AlertOccurrenceId { get; }
    public IngestionRunStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? FailureCode { get; private set; }
    public DateTimeOffset? WindowStart { get; private set; }
    public DateTimeOffset? WindowEnd { get; private set; }
    public IngestionSourceStatus LokiStatus { get; private set; } = IngestionSourceStatus.Pending;
    public IngestionSourceStatus TempoStatus { get; private set; } = IngestionSourceStatus.Pending;
    public int LogCount { get; private set; }
    public int TraceCount { get; private set; }
    public int ObservationCount { get; private set; }

    public static IngestionRun CreatePending(
        AlertOccurrenceId alertOccurrenceId,
        DateTimeOffset createdAt)
    {
        if (createdAt == default)
            throw new ArgumentException("The ingestion creation time is required.", nameof(createdAt));
        return new IngestionRun(IngestionRunId.New(), alertOccurrenceId, createdAt.ToUniversalTime());
    }

    public void Start(DateTimeOffset startedAt, DateTimeOffset windowStart, DateTimeOffset windowEnd)
    {
        if (Status is not (IngestionRunStatus.Pending or IngestionRunStatus.Running))
            throw new InvalidOperationException($"A {Status} ingestion run cannot be started.");
        if (windowStart == default || windowEnd == default || windowStart > windowEnd)
            throw new ArgumentException("A valid ingestion window is required.");

        Status = IngestionRunStatus.Running;
        AttemptCount++;
        StartedAt = startedAt.ToUniversalTime();
        UpdatedAt = StartedAt.Value;
        WindowStart = windowStart.ToUniversalTime();
        WindowEnd = windowEnd.ToUniversalTime();
        CompletedAt = null;
        FailureCode = null;
        LokiStatus = IngestionSourceStatus.Pending;
        TempoStatus = IngestionSourceStatus.Pending;
    }

    public void Complete(
        DateTimeOffset completedAt,
        IngestionSourceStatus lokiStatus,
        IngestionSourceStatus tempoStatus,
        int logCount,
        int traceCount,
        int observationCount)
    {
        if (Status != IngestionRunStatus.Running)
            throw new InvalidOperationException("Only a running ingestion can be completed.");
        if (logCount < 0 || traceCount < 0 || observationCount < 0)
            throw new ArgumentOutOfRangeException(nameof(observationCount));

        LokiStatus = lokiStatus;
        TempoStatus = tempoStatus;
        LogCount = logCount;
        TraceCount = traceCount;
        ObservationCount = observationCount;
        Status = lokiStatus == IngestionSourceStatus.Failed &&
                 tempoStatus is IngestionSourceStatus.Failed or IngestionSourceStatus.Skipped
            ? IngestionRunStatus.Failed
            : lokiStatus == IngestionSourceStatus.Failed || tempoStatus == IngestionSourceStatus.Failed
                ? IngestionRunStatus.Partial
                : IngestionRunStatus.Completed;
        FailureCode = Status == IngestionRunStatus.Failed ? "TelemetrySourcesUnavailable" : null;
        CompletedAt = completedAt.ToUniversalTime();
        UpdatedAt = CompletedAt.Value;
    }

    public void Fail(DateTimeOffset failedAt, string failureCode)
    {
        if (string.IsNullOrWhiteSpace(failureCode))
            throw new ArgumentException("A failure code is required.", nameof(failureCode));
        Status = IngestionRunStatus.Failed;
        FailureCode = failureCode.Trim();
        CompletedAt = failedAt.ToUniversalTime();
        UpdatedAt = CompletedAt.Value;
    }
}
