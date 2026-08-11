using Sentinel.Domain.Investigations;

namespace Sentinel.Application.Investigations;

public sealed class GetInvestigationUseCase(IInvestigationRepository repository)
{
    public Task<PersistedInvestigation?> ExecuteAsync(
        InvestigationRunId id,
        CancellationToken cancellationToken = default) =>
        repository.GetByIdAsync(id, cancellationToken);
}
