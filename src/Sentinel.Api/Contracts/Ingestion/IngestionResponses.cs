using Sentinel.Application.Ingestion;

namespace Sentinel.Api.Contracts.Ingestion;

public sealed record AcceptedIngestionRunResponse(
    Guid IngestionRunId,
    Guid AlertOccurrenceId,
    string AlertName,
    string Service,
    string Environment,
    string Status,
    bool WasCreated);

public sealed record AcceptPrometheusAlertsResponse(
    int NotificationsAccepted,
    int Created,
    int Duplicates,
    IReadOnlyList<AcceptedIngestionRunResponse> Runs)
{
    public static AcceptPrometheusAlertsResponse From(AcceptPrometheusAlertsResult result) => new(
        result.NotificationCount,
        result.CreatedCount,
        result.DuplicateCount,
        result.Ingestions.Select(item => new AcceptedIngestionRunResponse(
            item.Run.Id.Value,
            item.Alert.Id.Value,
            item.Alert.AlertName,
            item.Alert.Service,
            item.Alert.Environment,
            item.Run.Status.ToString(),
            item.WasCreated)).ToArray());
}

public sealed record IngestionRunResponse(
    Guid Id,
    Guid AlertOccurrenceId,
    string Status,
    int AttemptCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureCode,
    DateTimeOffset? WindowStart,
    DateTimeOffset? WindowEnd,
    string LokiStatus,
    string TempoStatus,
    int LogCount,
    int TraceCount,
    int ObservationCount,
    string AlertName,
    string Service,
    string Environment,
    DateTimeOffset AlertStartedAt,
    DateTimeOffset? AlertEndsAt,
    DateTimeOffset ReceivedAt)
{
    public static IngestionRunResponse From(PersistedIngestion ingestion) => new(
        ingestion.Run.Id.Value,
        ingestion.Alert.Id.Value,
        ingestion.Run.Status.ToString(),
        ingestion.Run.AttemptCount,
        ingestion.Run.CreatedAt,
        ingestion.Run.UpdatedAt,
        ingestion.Run.StartedAt,
        ingestion.Run.CompletedAt,
        ingestion.Run.FailureCode,
        ingestion.Run.WindowStart,
        ingestion.Run.WindowEnd,
        ingestion.Run.LokiStatus.ToString(),
        ingestion.Run.TempoStatus.ToString(),
        ingestion.Run.LogCount,
        ingestion.Run.TraceCount,
        ingestion.Run.ObservationCount,
        ingestion.Alert.AlertName,
        ingestion.Alert.Service,
        ingestion.Alert.Environment,
        ingestion.Alert.StartedAt,
        ingestion.Alert.EndsAt,
        ingestion.Alert.ReceivedAt);
}
