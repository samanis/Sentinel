using Sentinel.Application.Investigations;
using Sentinel.Domain.Investigations;

namespace Sentinel.Infrastructure.Investigations;

public sealed class InMemoryInvestigationRepository : IInvestigationRepository
{
    private readonly Dictionary<InvestigationRunId, InvestigationRun> runs = [];
    private readonly Dictionary<InvestigationRunId, IReadOnlyList<EvidenceRelationship>> storedRelationships = [];
    private readonly Dictionary<InvestigationRunId, IReadOnlyList<Hypothesis>> storedHypotheses = [];

    public IReadOnlyCollection<InvestigationRun> Runs => runs.Values;

    public Task AddAsync(InvestigationRun run, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        runs.Add(run.Id, run);
        return Task.CompletedTask;
    }

    public Task CompleteAsync(
        InvestigationRun run,
        IReadOnlyCollection<EvidenceRelationship> relationships,
        IReadOnlyCollection<Hypothesis> hypotheses,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        storedRelationships[run.Id] = relationships.ToArray();
        storedHypotheses[run.Id] = hypotheses.ToArray();
        return Task.CompletedTask;
    }

    public Task UpdateAsync(InvestigationRun run, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task<PersistedInvestigation?> GetByIdAsync(
        InvestigationRunId id,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!runs.TryGetValue(id, out var run))
            return Task.FromResult<PersistedInvestigation?>(null);
        return Task.FromResult<PersistedInvestigation?>(new PersistedInvestigation(
            run,
            storedRelationships.GetValueOrDefault(id) ?? [],
            storedHypotheses.GetValueOrDefault(id) ?? []));
    }
}
