using Sentinel.Domain.Ingestion;

namespace Sentinel.Application.Ingestion;

public sealed class GetIngestionRunUseCase(IAlertIngestionRepository repository)
{
    public Task<PersistedIngestion?> ExecuteAsync(
        IngestionRunId id,
        CancellationToken cancellationToken) =>
        repository.GetByRunIdAsync(id, cancellationToken);
}
