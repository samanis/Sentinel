using Sentinel.Domain.Ingestion;

namespace Sentinel.Application.Ingestion;

public sealed record BundleObservation(
    string SourceSystem,
    string SourceReference,
    DateTimeOffset ObservedAt,
    string Summary,
    string? TraceId,
    string Service);

public sealed record EvidenceBundleCandidate(
    Guid BundleId,
    IngestionRunId IngestionRunId,
    string AlertName,
    string Service,
    string Environment,
    string? Scenario,
    bool IsSimulated,
    DateTimeOffset AlertStartedAt,
    IReadOnlyList<BundleObservation> Observations);

public sealed record SimilarEvidenceBundle(
    Guid Id,
    IngestionRunId IngestionRunId,
    string AlertName,
    string Service,
    string Environment,
    string SearchDocument,
    string EmbeddingModel,
    double Similarity,
    DateTimeOffset CreatedAt);

public interface IEvidenceBundleRepository
{
    Task<EvidenceBundleCandidate?> ClaimNextAsync(DateTimeOffset claimedAt, CancellationToken cancellationToken);
    Task CompleteAsync(
        Guid bundleId, string searchDocument, string embeddingModel,
        float[] embedding, DateTimeOffset completedAt, CancellationToken cancellationToken);
    Task FailAsync(Guid bundleId, string failureCode, DateTimeOffset failedAt, CancellationToken cancellationToken);
    Task<IReadOnlyList<SimilarEvidenceBundle>> SearchAsync(
        float[] embedding, string embeddingModel, string? service,
        string? environment, int limit, CancellationToken cancellationToken);
}
