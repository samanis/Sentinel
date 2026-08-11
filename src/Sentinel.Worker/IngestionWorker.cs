using Microsoft.Extensions.Options;
using Sentinel.Application.Ingestion;

namespace Sentinel.Worker;

public sealed class IngestionWorkerOptions
{
    public int PollIntervalSeconds { get; set; } = 2;
}

public sealed partial class IngestionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<IngestionWorkerOptions> options,
    ILogger<IngestionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Clamp(options.Value.PollIntervalSeconds, 1, 60));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var result = await scope.ServiceProvider
                    .GetRequiredService<ProcessNextIngestionUseCase>()
                    .ExecuteAsync(stoppingToken);
                if (!result.WorkClaimed)
                {
                    var bundleResult = await scope.ServiceProvider
                        .GetRequiredService<ProcessNextEvidenceBundleUseCase>()
                        .ExecuteAsync(stoppingToken);
                    if (!bundleResult.WorkClaimed)
                    {
                        await Task.Delay(delay, stoppingToken);
                        continue;
                    }
                    LogBundleCompleted(
                        logger, bundleResult.BundleId!.Value, bundleResult.ObservationCount);
                    continue;
                }

                LogCompleted(
                    logger, result.RunId!.Value.Value, result.Status!.Value.ToString(),
                    result.LogCount, result.TraceCount, result.ObservationCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogFailure(logger, exception);
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    [LoggerMessage(1, LogLevel.Information,
        "IngestionRunProcessed RunId={RunId} Status={Status} Logs={LogCount} Traces={TraceCount} Observations={ObservationCount}")]
    private static partial void LogCompleted(
        ILogger logger, Guid runId, string status, int logCount, int traceCount, int observationCount);

    [LoggerMessage(2, LogLevel.Error, "IngestionWorkerIterationFailed")]
    private static partial void LogFailure(ILogger logger, Exception exception);

    [LoggerMessage(3, LogLevel.Information,
        "EvidenceBundleEmbedded BundleId={BundleId} Observations={ObservationCount}")]
    private static partial void LogBundleCompleted(ILogger logger, Guid bundleId, int observationCount);
}
