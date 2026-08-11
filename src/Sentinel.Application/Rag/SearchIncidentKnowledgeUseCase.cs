using Sentinel.Application.AI;

namespace Sentinel.Application.Rag;

public sealed class SearchIncidentKnowledgeUseCase(
    IRagKnowledgeRepository repository,
    IEmbeddingClient embeddingClient)
{
    public async Task<IReadOnlyList<RagEvidenceMatch>> ExecuteAsync(
        string query,
        string? service,
        string? environment,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 4_000)
            throw new ArgumentException("A query containing at most 4000 characters is required.", nameof(query));
        if (limit is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(limit), "The result limit must be between 1 and 20.");

        var recentIntent = HasRecentIntent(query);
        if (recentIntent)
        {
            var recent = await repository.SearchRecentAsync(
                Normalize(service), Normalize(environment), 100, cancellationToken);
            var statusCode = ExtractHttpStatusCode(query);
            var matchingStatus = statusCode is null
                ? recent
                : recent.Where(item => ContainsHttpStatus(item.SearchDocument, statusCode)).ToArray();
            var pool = matchingStatus.Count > 0 ? matchingStatus : recent;
            return pool.Take(limit).ToArray();
        }

        var embedding = await embeddingClient.EmbedAsync(query.Trim(), cancellationToken);
        var candidates = await repository.SearchAsync(
            embedding.Vector,
            embedding.Model,
            Normalize(service),
            Normalize(environment),
            Math.Min(limit * 5, 50),
            cancellationToken);
        return candidates
            .OrderByDescending(item => HybridScore(query, item))
            .ThenByDescending(item => item.Similarity)
            .Take(limit)
            .ToArray();
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool HasRecentIntent(string query) =>
        query.Contains("most recent", StringComparison.OrdinalIgnoreCase) ||
        query.Contains("mos recent", StringComparison.OrdinalIgnoreCase) ||
        query.Contains("latest", StringComparison.OrdinalIgnoreCase) ||
        query.Contains("newest", StringComparison.OrdinalIgnoreCase);

    private static string? ExtractHttpStatusCode(string query) =>
        query.Split([' ', '?', '.', ',', ';', ':'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(token => token.Length == 3 && token.All(char.IsAsciiDigit) && token[0] is '4' or '5');

    private static bool ContainsHttpStatus(string document, string statusCode) =>
        document.Contains($"HTTP={statusCode}", StringComparison.OrdinalIgnoreCase) ||
        document.Contains($"HTTP status: {statusCode}", StringComparison.OrdinalIgnoreCase);

    private static double HybridScore(string query, RagEvidenceMatch candidate)
    {
        var score = candidate.Similarity;
        var document = candidate.SearchDocument;
        var queryMentionsDatabase = query.Contains("database", StringComparison.OrdinalIgnoreCase);
        var queryAsksForQuery = query.Contains("query", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("sql", StringComparison.OrdinalIgnoreCase);
        var queryAsksForDelay = query.Contains("slow", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("delay", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("timeout", StringComparison.OrdinalIgnoreCase);

        if (queryMentionsDatabase && document.Contains("Cause=database", StringComparison.OrdinalIgnoreCase))
            score += 0.08;
        if (queryAsksForQuery &&
            (document.Contains("SimulatedStatement=", StringComparison.OrdinalIgnoreCase) ||
             document.Contains("DiagnosticStatement=", StringComparison.OrdinalIgnoreCase) ||
             document.Contains("Database query:", StringComparison.OrdinalIgnoreCase)))
            score += 0.15;
        if (queryAsksForDelay && document.Contains("Scenario SlowDatabase", StringComparison.OrdinalIgnoreCase))
            score += 0.05;

        return score;
    }
}
