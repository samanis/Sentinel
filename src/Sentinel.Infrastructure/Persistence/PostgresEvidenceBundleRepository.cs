using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using System.Text.Json;
using Sentinel.Application.Ingestion;
using Sentinel.Domain.Ingestion;

namespace Sentinel.Infrastructure.Persistence;

public sealed class PostgresEvidenceBundleRepository(SentinelDbContext dbContext)
    : IEvidenceBundleRepository
{
    private const double ClusterSimilarityThreshold = 0.85;
    public async Task<EvidenceBundleCandidate?> ClaimNextAsync(
        DateTimeOffset claimedAt, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var run = await dbContext.IngestionRuns.FromSqlRaw("""
            SELECT r.* FROM ingestion_runs r
            WHERE r.status IN ('Completed', 'Partial')
              AND NOT EXISTS (
                  SELECT 1 FROM evidence_bundles b WHERE b.ingestion_run_id = r.id)
            ORDER BY r.completed_at, r.created_at
            FOR UPDATE SKIP LOCKED
            LIMIT 1
            """).SingleOrDefaultAsync(cancellationToken);
        if (run is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var alert = await dbContext.AlertOccurrences.AsNoTracking()
            .SingleAsync(item => item.Id == run.AlertOccurrenceId, cancellationToken);
        var observations = await dbContext.IngestionObservations.AsNoTracking()
            .Where(item => item.IngestionRunId == run.Id)
            .OrderBy(item => item.ObservedAt)
            .Select(item => new BundleObservation(
                item.SourceSystem, item.SourceReference, item.ObservedAt,
                item.Summary, item.TraceId, item.Service))
            .ToListAsync(cancellationToken);
        var bundle = EvidenceBundleRecord.CreatePending(
            run.Id, alert.AlertName, alert.Service, alert.Environment, claimedAt);
        dbContext.Set<EvidenceBundleRecord>().Add(bundle);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var scenario = ReadLabel(alert.LabelsJson, "incidentlab_scenario");
        var explicitlySimulated = ReadLabel(alert.LabelsJson, "incidentlab_simulated");
        return new EvidenceBundleCandidate(
            bundle.Id, run.Id, alert.AlertName, alert.Service, alert.Environment,
            scenario,
            string.Equals(explicitlySimulated, "true", StringComparison.OrdinalIgnoreCase) || scenario is not null,
            alert.StartedAt, observations);
    }

    public async Task CompleteAsync(
        Guid bundleId, string searchDocument, string embeddingModel,
        float[] embedding, DateTimeOffset completedAt, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var bundle = await dbContext.Set<EvidenceBundleRecord>()
            .SingleAsync(item => item.Id == bundleId, cancellationToken);
        bundle.Complete(searchDocument, embeddingModel, embedding, completedAt);
        var run = await dbContext.IngestionRuns.AsNoTracking()
            .SingleAsync(item => item.Id == bundle.IngestionRunId, cancellationToken);
        var alert = await dbContext.AlertOccurrences.AsNoTracking()
            .SingleAsync(item => item.Id == run.AlertOccurrenceId, cancellationToken);

        var vector = new Vector(embedding);
        var nearest = await dbContext.IncidentClusters
            .Where(item => item.Service == bundle.Service &&
                           item.Environment == bundle.Environment &&
                           item.EmbeddingModel == embeddingModel)
            .Select(item => new
            {
                Cluster = item,
                Distance = item.RepresentativeEmbedding.CosineDistance(vector)
            })
            .OrderBy(item => item.Distance)
            .FirstOrDefaultAsync(cancellationToken);
        var similarity = nearest is null ? 1d : 1d - nearest.Distance;
        IncidentClusterRecord cluster;
        if (nearest is null || similarity < ClusterSimilarityThreshold)
        {
            cluster = IncidentClusterRecord.Create(
                bundle.Service, bundle.Environment, embeddingModel, embedding, alert.StartedAt);
            dbContext.IncidentClusters.Add(cluster);
            similarity = 1d;
        }
        else
        {
            cluster = nearest.Cluster;
            cluster.AddOccurrence(alert.StartedAt);
        }

        dbContext.IncidentClusterOccurrences.Add(
            IncidentClusterOccurrenceRecord.Create(cluster.Id, bundle.Id, similarity, alert.StartedAt));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task FailAsync(
        Guid bundleId, string failureCode, DateTimeOffset failedAt, CancellationToken cancellationToken)
    {
        var bundle = await dbContext.Set<EvidenceBundleRecord>()
            .SingleAsync(item => item.Id == bundleId, cancellationToken);
        bundle.Fail(failureCode, failedAt);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SimilarEvidenceBundle>> SearchAsync(
        float[] embedding, string embeddingModel, string? service,
        string? environment, int limit, CancellationToken cancellationToken)
    {
        var queryVector = new Vector(embedding);
        var query = dbContext.Set<EvidenceBundleRecord>().AsNoTracking()
            .Where(item => item.Status == "Completed" &&
                           item.Embedding != null &&
                           item.EmbeddingModel == embeddingModel);
        if (service is not null) query = query.Where(item => item.Service == service);
        if (environment is not null) query = query.Where(item => item.Environment == environment);
        var matches = await query
            .Select(item => new
            {
                item.Id,
                item.IngestionRunId,
                item.AlertName,
                item.Service,
                item.Environment,
                item.SearchDocument,
                item.EmbeddingModel,
                item.CreatedAt,
                Distance = item.Embedding!.CosineDistance(queryVector)
            })
            .OrderBy(item => item.Distance)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return matches.Select(item => new SimilarEvidenceBundle(
            item.Id, item.IngestionRunId, item.AlertName, item.Service,
            item.Environment, item.SearchDocument, item.EmbeddingModel!,
            1d - item.Distance, item.CreatedAt)).ToArray();
    }

    private static string? ReadLabel(string labelsJson, string name)
    {
        using var document = JsonDocument.Parse(labelsJson);
        return document.RootElement.TryGetProperty(name, out var value) &&
               value.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;
    }
}
