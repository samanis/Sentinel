using Sentinel.Domain.Ingestion;

namespace Sentinel.Application.Ingestion;

public sealed record IngestionCollectionResult(
    IngestionSourceStatus LokiStatus,
    IngestionSourceStatus TempoStatus,
    int LogCount,
    int TraceCount);

public interface IIngestionWorkRepository
{
    Task<PersistedIngestion?> ClaimNextAsync(
        DateTimeOffset claimedAt,
        DateTimeOffset staleBefore,
        TimeSpan beforeAlert,
        TimeSpan afterAlert,
        CancellationToken cancellationToken);

    Task CompleteAsync(
        IngestionRunId runId,
        IngestionCollectionResult result,
        IReadOnlyCollection<IngestionObservation> observations,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken);

    Task FailAsync(
        IngestionRunId runId,
        string failureCode,
        DateTimeOffset failedAt,
        CancellationToken cancellationToken);
}
