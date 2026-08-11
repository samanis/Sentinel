using System.Text;
using Sentinel.Application.AI;

namespace Sentinel.Application.Ingestion;

public sealed record ProcessNextEvidenceBundleResult(bool WorkClaimed, Guid? BundleId, int ObservationCount);

public sealed class ProcessNextEvidenceBundleUseCase(
    IEvidenceBundleRepository repository,
    IEmbeddingClient embeddingClient,
    Sentinel.Application.Abstractions.IClock clock)
{
    private const int MaximumObservations = 250;
    private const int MaximumDocumentLength = 32_000;

    public async Task<ProcessNextEvidenceBundleResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var candidate = await repository.ClaimNextAsync(clock.UtcNow, cancellationToken);
        if (candidate is null) return new(false, null, 0);

        try
        {
            var document = BuildSearchDocument(candidate);
            var embedding = await embeddingClient.EmbedAsync(document, cancellationToken);
            await repository.CompleteAsync(
                candidate.BundleId, document, embedding.Model, embedding.Vector,
                clock.UtcNow, cancellationToken);
            return new(true, candidate.BundleId, candidate.Observations.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            await repository.FailAsync(
                candidate.BundleId, "EmbeddingFailed", clock.UtcNow, CancellationToken.None);
            throw;
        }
    }

    internal static string BuildSearchDocument(EvidenceBundleCandidate candidate)
    {
        var builder = new StringBuilder();
        builder.Append("Alert: ").AppendLine(candidate.AlertName);
        builder.Append("Service: ").AppendLine(candidate.Service);
        builder.Append("Environment: ").AppendLine(candidate.Environment);
        builder.Append("Scenario: ").AppendLine(candidate.Scenario ?? "unknown");
        builder.Append("Simulation: ").AppendLine(candidate.IsSimulated ? "true" : "false");
        builder.Append("Started: ").AppendLine(candidate.AlertStartedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        builder.AppendLine("Evidence:");
        var scenarioObservations = FilterByScenario(candidate.Observations, candidate.Scenario);
        var relevantObservations = SelectRelevantObservations(
            scenarioObservations, candidate.AlertStartedAt);
        foreach (var item in relevantObservations)
        {
            builder.Append("- [").Append(item.SourceSystem).Append("] ")
                .Append(item.Summary);
            if (!string.IsNullOrWhiteSpace(item.TraceId))
                builder.Append(" TraceId=").Append(item.TraceId);
            builder.AppendLine();
            if (builder.Length >= MaximumDocumentLength) break;
        }

        return builder.ToString()[..Math.Min(builder.Length, MaximumDocumentLength)];
    }

    private static List<BundleObservation> SelectRelevantObservations(
        IReadOnlyList<BundleObservation> observations,
        DateTimeOffset alertStartedAt)
    {
        var sources = observations
            .GroupBy(value => value.SourceSystem, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new Queue<BundleObservation>(group
                .OrderBy(value => DistanceFromAlert(value.ObservedAt, alertStartedAt))
                .ThenBy(value => value.SourceReference, StringComparer.Ordinal)))
            .ToArray();
        var selected = new List<BundleObservation>(Math.Min(MaximumObservations, observations.Count));

        while (selected.Count < MaximumObservations && sources.Any(source => source.Count > 0))
        {
            foreach (var source in sources)
            {
                if (source.Count > 0) selected.Add(source.Dequeue());
                if (selected.Count == MaximumObservations) break;
            }
        }

        return selected;
    }

    private static long DistanceFromAlert(DateTimeOffset observedAt, DateTimeOffset alertStartedAt)
    {
        var ticks = (observedAt - alertStartedAt).Ticks;
        return ticks == long.MinValue ? long.MaxValue : Math.Abs(ticks);
    }

    private static IReadOnlyList<BundleObservation> FilterByScenario(
        IReadOnlyList<BundleObservation> observations,
        string? scenario)
    {
        if (string.IsNullOrWhiteSpace(scenario)) return observations;

        var matching = observations.Where(item =>
            item.Summary.Contains($"Scenario {scenario}", StringComparison.OrdinalIgnoreCase) ||
            item.Summary.Contains($"Scenario: {scenario}", StringComparison.OrdinalIgnoreCase)).ToArray();
        return matching.Length == 0 ? observations : matching;
    }
}
