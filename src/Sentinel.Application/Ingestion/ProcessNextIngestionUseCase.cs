using Sentinel.Application.Abstractions;
using Sentinel.Application.Evidence.LogIngestion;
using Sentinel.Application.Evidence.TraceIngestion;
using Sentinel.Domain.Ingestion;
using System.Text.Json;

namespace Sentinel.Application.Ingestion;

public sealed record ProcessNextIngestionResult(
    bool WorkClaimed,
    IngestionRunId? RunId,
    IngestionRunStatus? Status,
    int LogCount,
    int TraceCount,
    int ObservationCount);

public sealed class ProcessNextIngestionUseCase(
    IIngestionWorkRepository repository,
    ILogSource logSource,
    ITraceSource traceSource,
    ILogEvidenceNormalizer logNormalizer,
    ITraceEvidenceNormalizer traceNormalizer,
    IClock clock)
{
    private static readonly TimeSpan BeforeAlert = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AfterAlert = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StaleClaimTimeout = TimeSpan.FromMinutes(2);
    private const int LogLimit = 500;
    private const int TraceLimit = 20;

    public async Task<ProcessNextIngestionResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var claimed = await repository.ClaimNextAsync(
            now, now - StaleClaimTimeout, BeforeAlert, AfterAlert, cancellationToken);
        if (claimed is null) return new(false, null, null, 0, 0, 0);

        try
        {
            IReadOnlyList<LogObservation> logs = [];
            var lokiStatus = IngestionSourceStatus.Succeeded;
            try
            {
                logs = await logSource.QueryAsync(new LogQuery(
                    claimed.Alert.Service,
                    claimed.Run.WindowStart!.Value,
                    claimed.Run.WindowEnd!.Value,
                    LogLimit), cancellationToken);
            }
            catch (LogSourceException)
            {
                lokiStatus = IngestionSourceStatus.Failed;
            }

            var normalizedLogs = logNormalizer.Normalize(logs);
            var observations = normalizedLogs.Select(item => IngestionObservation.Create(
                claimed.Run.Id, "Loki", item.SourceReference, item.ObservedAt, item.Summary,
                item.SourceTraceId, item.SourceSpanId, item.SourceService, now)).ToList();

            var scenario = ReadLabel(claimed.Alert.LabelsJson, "incidentlab_scenario");
            var traceIds = logs
                .OrderByDescending(item => MatchesScenario(item, scenario))
                .ThenBy(item => DistanceFromAlert(item.ObservedAt, claimed.Alert.StartedAt))
                .Select(item => item.TraceId)
                .Where(IsTraceId)
                .Select(item => item!.ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .Take(TraceLimit)
                .ToArray();
            var tempoStatus = lokiStatus == IngestionSourceStatus.Failed
                ? IngestionSourceStatus.Skipped
                : IngestionSourceStatus.Succeeded;
            var traceCount = 0;

            foreach (var traceId in traceIds)
            {
                try
                {
                    var trace = await traceSource.GetTraceAsync(traceId, cancellationToken);
                    if (trace is null) continue;
                    traceCount++;
                    observations.AddRange(traceNormalizer.Normalize(trace).Select(item =>
                        IngestionObservation.Create(
                            claimed.Run.Id, "Tempo", item.SourceReference, item.ObservedAt,
                            item.Summary, item.SourceTraceId, item.SourceSpanId,
                            item.SourceService, now)));
                }
                catch (TraceSourceException)
                {
                    tempoStatus = IngestionSourceStatus.Failed;
                }
            }

            var collection = new IngestionCollectionResult(lokiStatus, tempoStatus, logs.Count, traceCount);
            await repository.CompleteAsync(
                claimed.Run.Id, collection, observations, clock.UtcNow, cancellationToken);
            var finalStatus = lokiStatus == IngestionSourceStatus.Failed
                ? IngestionRunStatus.Failed
                : tempoStatus == IngestionSourceStatus.Failed
                    ? IngestionRunStatus.Partial
                    : IngestionRunStatus.Completed;
            return new(true, claimed.Run.Id, finalStatus, logs.Count, traceCount, observations.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            await repository.FailAsync(
                claimed.Run.Id, "UnhandledWorkerFailure", clock.UtcNow, CancellationToken.None);
            throw;
        }
    }

    private static bool IsTraceId(string? value) =>
        value is { Length: 32 } && value.All(Uri.IsHexDigit);

    private static bool MatchesScenario(LogObservation log, string? scenario) =>
        !string.IsNullOrWhiteSpace(scenario) &&
        (log.Body.Contains($"Scenario {scenario}", StringComparison.OrdinalIgnoreCase) ||
         log.Attributes.Values.Any(value => value.Equals(scenario, StringComparison.OrdinalIgnoreCase)));

    private static long DistanceFromAlert(DateTimeOffset observedAt, DateTimeOffset alertStartedAt)
    {
        var ticks = (observedAt - alertStartedAt).Ticks;
        return ticks == long.MinValue ? long.MaxValue : Math.Abs(ticks);
    }

    private static string? ReadLabel(string labelsJson, string name)
    {
        using var document = JsonDocument.Parse(labelsJson);
        return document.RootElement.TryGetProperty(name, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
