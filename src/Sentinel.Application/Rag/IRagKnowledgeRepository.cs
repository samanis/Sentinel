namespace Sentinel.Application.Rag;

public sealed record RagEvidenceMatch(
    Guid BundleId,
    Guid IngestionRunId,
    string AlertName,
    string Service,
    string Environment,
    string SearchDocument,
    string EmbeddingModel,
    double Similarity,
    DateTimeOffset CreatedAt,
    Guid? ClusterId = null,
    int OccurrenceCount = 1,
    int OccurrencesLastHour = 1,
    DateTimeOffset? FirstSeenAt = null,
    DateTimeOffset? LastSeenAt = null);

public interface IRagKnowledgeRepository
{
    Task<IReadOnlyList<RagEvidenceMatch>> SearchRecentAsync(
        string? service,
        string? environment,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RagEvidenceMatch>> SearchAsync(
        float[] embedding,
        string embeddingModel,
        string? service,
        string? environment,
        int limit,
        CancellationToken cancellationToken);
}
