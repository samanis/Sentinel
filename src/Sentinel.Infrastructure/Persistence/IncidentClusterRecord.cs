using Pgvector;

namespace Sentinel.Infrastructure.Persistence;

public sealed class IncidentClusterRecord
{
    private IncidentClusterRecord() { }

    public Guid Id { get; private set; }
    public string Service { get; private set; } = string.Empty;
    public string Environment { get; private set; } = string.Empty;
    public string EmbeddingModel { get; private set; } = string.Empty;
    public Vector RepresentativeEmbedding { get; private set; } = null!;
    public int OccurrenceCount { get; private set; }
    public DateTimeOffset FirstSeenAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }

    public static IncidentClusterRecord Create(
        string service, string environment, string embeddingModel,
        float[] embedding, DateTimeOffset occurredAt) => new()
        {
            Id = Guid.NewGuid(), Service = service, Environment = environment,
            EmbeddingModel = embeddingModel, RepresentativeEmbedding = new Vector(embedding),
            OccurrenceCount = 1, FirstSeenAt = occurredAt, LastSeenAt = occurredAt
        };

    public void AddOccurrence(DateTimeOffset occurredAt)
    {
        OccurrenceCount++;
        if (occurredAt < FirstSeenAt) FirstSeenAt = occurredAt;
        if (occurredAt > LastSeenAt) LastSeenAt = occurredAt;
    }
}

public sealed class IncidentClusterOccurrenceRecord
{
    private IncidentClusterOccurrenceRecord() { }
    public Guid Id { get; private set; }
    public Guid ClusterId { get; private set; }
    public Guid EvidenceBundleId { get; private set; }
    public double Similarity { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    public static IncidentClusterOccurrenceRecord Create(
        Guid clusterId, Guid bundleId, double similarity, DateTimeOffset occurredAt) => new()
        {
            Id = Guid.NewGuid(), ClusterId = clusterId, EvidenceBundleId = bundleId,
            Similarity = similarity, OccurredAt = occurredAt
        };
}
