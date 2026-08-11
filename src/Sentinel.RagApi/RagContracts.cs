using Sentinel.Application.Rag;

namespace Sentinel.RagApi;

public sealed record RagSearchRequest(
    string Query,
    string? Service = null,
    string? Environment = null,
    int Limit = 5);

public sealed record RagQueryRequest(
    string Question,
    string? Service = null,
    string? Environment = null,
    int Limit = 5);

public sealed record RagMatchResponse(
    Guid BundleId,
    Guid IngestionRunId,
    string AlertName,
    string Service,
    string Environment,
    string Content,
    string EmbeddingModel,
    double Similarity,
    DateTimeOffset CreatedAt)
{
    public static RagMatchResponse From(RagEvidenceMatch item) => new(
        item.BundleId, item.IngestionRunId, item.AlertName, item.Service,
        item.Environment, item.SearchDocument, item.EmbeddingModel,
        item.Similarity, item.CreatedAt);
}

public sealed record RagSearchResponse(IReadOnlyList<RagMatchResponse> Matches);

public sealed record RagSourceResponse(
    Guid BundleId,
    Guid IngestionRunId,
    string AlertName,
    string Service,
    string Environment,
    string? Scenario,
    bool IsSimulated,
    Guid? ClusterId,
    int OccurrenceCount,
    int OccurrencesLastHour,
    DateTimeOffset? FirstSeenAt,
    DateTimeOffset? LastSeenAt,
    double Similarity,
    DateTimeOffset CreatedAt,
    string LogSummary,
    string TraceSummary,
    IReadOnlyList<string> LogContents,
    IReadOnlyList<string> TraceContents)
{
    public static RagSourceResponse From(RagSource item) => new(
        item.BundleId, item.IngestionRunId, item.AlertName, item.Service,
        item.Environment, item.Scenario, item.IsSimulated,
        item.ClusterId, item.OccurrenceCount, item.OccurrencesLastHour,
        item.FirstSeenAt, item.LastSeenAt, item.Similarity, item.CreatedAt,
        item.LogSummary, item.TraceSummary,
        item.LogContents, item.TraceContents);
}

public sealed record RagQueryResponse(
    string Answer,
    string? Model,
    IReadOnlyList<RagSourceResponse> Sources)
{
    public static RagQueryResponse From(RagAnswer item) => new(
        item.Answer, item.Model, item.Sources.Select(RagSourceResponse.From).ToArray());
}
