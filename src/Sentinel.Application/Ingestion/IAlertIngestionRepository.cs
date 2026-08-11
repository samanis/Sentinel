using Sentinel.Domain.Ingestion;

namespace Sentinel.Application.Ingestion;

public sealed record AcceptedIngestion(
    AlertOccurrence Alert,
    IngestionRun Run,
    bool WasCreated);

public sealed record PersistedIngestion(
    AlertOccurrence Alert,
    IngestionRun Run);

public interface IAlertIngestionRepository
{
    Task<IReadOnlyList<AcceptedIngestion>> AcceptAsync(
        IReadOnlyCollection<AlertOccurrence> alerts,
        DateTimeOffset acceptedAt,
        CancellationToken cancellationToken);

    Task<PersistedIngestion?> GetByRunIdAsync(
        IngestionRunId runId,
        CancellationToken cancellationToken);

}
