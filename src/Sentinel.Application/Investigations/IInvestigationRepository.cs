using Sentinel.Domain.Investigations;

namespace Sentinel.Application.Investigations;

public sealed record PersistedInvestigation(
    InvestigationRun Run,
    IReadOnlyList<EvidenceRelationship> Relationships,
    IReadOnlyList<Hypothesis> Hypotheses);

public interface IInvestigationRepository
{
    Task AddAsync(InvestigationRun run, CancellationToken cancellationToken);

    Task CompleteAsync(
        InvestigationRun run,
        IReadOnlyCollection<EvidenceRelationship> relationships,
        IReadOnlyCollection<Hypothesis> hypotheses,
        CancellationToken cancellationToken);

    Task UpdateAsync(InvestigationRun run, CancellationToken cancellationToken);

    Task<PersistedInvestigation?> GetByIdAsync(
        InvestigationRunId id,
        CancellationToken cancellationToken);
}
