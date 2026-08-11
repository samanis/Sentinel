using Pgvector;
using Sentinel.Domain.Ingestion;

namespace Sentinel.Infrastructure.Persistence;

internal sealed class EvidenceBundleRecord
{
    private EvidenceBundleRecord() { }

    private EvidenceBundleRecord(
        Guid id, IngestionRunId ingestionRunId, string alertName,
        string service, string environment, DateTimeOffset createdAt)
    {
        Id = id;
        IngestionRunId = ingestionRunId;
        AlertName = alertName;
        Service = service;
        Environment = environment;
        Status = "Pending";
        SearchDocument = string.Empty;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public IngestionRunId IngestionRunId { get; private set; }
    public string AlertName { get; private set; } = string.Empty;
    public string Service { get; private set; } = string.Empty;
    public string Environment { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string SearchDocument { get; private set; } = string.Empty;
    public Vector? Embedding { get; private set; }
    public string? EmbeddingModel { get; private set; }
    public int? EmbeddingDimensions { get; private set; }
    public string? FailureCode { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public static EvidenceBundleRecord CreatePending(
        IngestionRunId runId, string alertName, string service,
        string environment, DateTimeOffset createdAt) =>
        new(Guid.NewGuid(), runId, alertName, service, environment, createdAt.ToUniversalTime());

    public void Complete(
        string searchDocument, string model, float[] vector, DateTimeOffset completedAt)
    {
        SearchDocument = searchDocument;
        EmbeddingModel = model;
        EmbeddingDimensions = vector.Length;
        Embedding = new Vector(vector);
        Status = "Completed";
        FailureCode = null;
        CompletedAt = completedAt.ToUniversalTime();
        UpdatedAt = CompletedAt.Value;
    }

    public void Fail(string failureCode, DateTimeOffset failedAt)
    {
        Status = "Failed";
        FailureCode = failureCode;
        UpdatedAt = failedAt.ToUniversalTime();
        CompletedAt = UpdatedAt;
    }
}
