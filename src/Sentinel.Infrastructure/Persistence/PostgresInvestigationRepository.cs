using Microsoft.EntityFrameworkCore;
using Sentinel.Application.Investigations;
using Sentinel.Domain.Investigations;

namespace Sentinel.Infrastructure.Persistence;

public sealed class PostgresInvestigationRepository(SentinelDbContext dbContext)
    : IInvestigationRepository
{
    public async Task AddAsync(InvestigationRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        dbContext.InvestigationRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteAsync(
        InvestigationRun run,
        IReadOnlyCollection<EvidenceRelationship> relationships,
        IReadOnlyCollection<Hypothesis> hypotheses,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(relationships);
        ArgumentNullException.ThrowIfNull(hypotheses);
        if (run.Status != InvestigationRunStatus.Completed)
            throw new ArgumentException("The investigation run must be completed before persisting results.", nameof(run));
        if (relationships.Any(item => item.InvestigationRunId != run.Id) ||
            hypotheses.Any(item => item.InvestigationRunId != run.Id))
            throw new ArgumentException("All RCA results must belong to the investigation run.");

        dbContext.EvidenceRelationships.AddRange(relationships);
        dbContext.Hypotheses.AddRange(hypotheses);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task UpdateAsync(InvestigationRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PersistedInvestigation?> GetByIdAsync(
        InvestigationRunId id,
        CancellationToken cancellationToken)
    {
        var run = await dbContext.InvestigationRuns.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (run is null) return null;
        var hypotheses = await dbContext.Hypotheses.AsNoTracking()
            .Include(item => item.EvidenceReferences)
            .Where(item => item.InvestigationRunId == id)
            .OrderByDescending(item => item.Confidence)
            .ThenBy(item => item.CreatedAt)
            .ToArrayAsync(cancellationToken);
        var relationships = await dbContext.EvidenceRelationships.AsNoTracking()
            .Where(item => item.InvestigationRunId == id)
            .OrderBy(item => item.CreatedAt)
            .ToArrayAsync(cancellationToken);
        return new PersistedInvestigation(run, relationships, hypotheses);
    }
}
