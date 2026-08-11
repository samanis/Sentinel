using Microsoft.EntityFrameworkCore;
using Sentinel.Domain.Evidence;
using Sentinel.Domain.Incidents;
using Sentinel.Domain.Investigations;
using Sentinel.Domain.Ingestion;
using Pgvector.EntityFrameworkCore;

namespace Sentinel.Infrastructure.Persistence;

public sealed class SentinelDbContext(DbContextOptions<SentinelDbContext> options)
    : DbContext(options)
{
    public DbSet<Incident> Incidents => Set<Incident>();

    public DbSet<EvidenceItem> Evidence => Set<EvidenceItem>();
    public DbSet<InvestigationRun> InvestigationRuns => Set<InvestigationRun>();
    public DbSet<Hypothesis> Hypotheses => Set<Hypothesis>();
    public DbSet<EvidenceRelationship> EvidenceRelationships => Set<EvidenceRelationship>();
    public DbSet<AlertOccurrence> AlertOccurrences => Set<AlertOccurrence>();
    public DbSet<IngestionRun> IngestionRuns => Set<IngestionRun>();
    public DbSet<IngestionObservation> IngestionObservations => Set<IngestionObservation>();
    public DbSet<AlertNotificationRecord> AlertNotifications => Set<AlertNotificationRecord>();
    public DbSet<IncidentClusterRecord> IncidentClusters => Set<IncidentClusterRecord>();
    public DbSet<IncidentClusterOccurrenceRecord> IncidentClusterOccurrences => Set<IncidentClusterOccurrenceRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SentinelDbContext).Assembly);
    }
}
