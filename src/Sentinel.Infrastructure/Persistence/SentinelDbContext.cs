using Microsoft.EntityFrameworkCore;
using Sentinel.Domain.Evidence;
using Sentinel.Domain.Incidents;
using Sentinel.Domain.Investigations;

namespace Sentinel.Infrastructure.Persistence;

public sealed class SentinelDbContext(DbContextOptions<SentinelDbContext> options)
    : DbContext(options)
{
    public DbSet<Incident> Incidents => Set<Incident>();

    public DbSet<EvidenceItem> Evidence => Set<EvidenceItem>();
    public DbSet<InvestigationRun> InvestigationRuns => Set<InvestigationRun>();
    public DbSet<Hypothesis> Hypotheses => Set<Hypothesis>();
    public DbSet<EvidenceRelationship> EvidenceRelationships => Set<EvidenceRelationship>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SentinelDbContext).Assembly);
    }
}
