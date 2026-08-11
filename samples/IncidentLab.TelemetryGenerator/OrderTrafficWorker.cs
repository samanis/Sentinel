using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace IncidentLab.TelemetryGenerator;

public sealed partial class OrderTrafficWorker(
    IHttpClientFactory httpClientFactory,
    IOptions<TelemetryGeneratorOptions> options,
    OrderIdSequence orderIds,
    GeneratorState state,
    TelemetryGeneratorTelemetry telemetry,
    TimeProvider timeProvider,
    ILogger<OrderTrafficWorker> logger) : BackgroundService
{
    public const string HttpClientName = "IncidentLabOrderApi";

    private readonly TimeSpan interval = TimeSpan.FromSeconds(1d / options.Value.RequestsPerSecond);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        state.MarkRunning();
        GeneratorStarted(logger, options.Value.TargetBaseUrl, options.Value.RequestsPerSecond);

        while (!stoppingToken.IsCancellationRequested)
        {
            var iterationStartedAt = timeProvider.GetTimestamp();
            await SendOrderRequestAsync(stoppingToken);
            var elapsed = timeProvider.GetElapsedTime(iterationStartedAt);
            var delay = interval - elapsed;

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, timeProvider, stoppingToken);
            }
        }
    }

    private async Task SendOrderRequestAsync(CancellationToken cancellationToken)
    {
        var orderId = orderIds.Next();
        var observedAt = timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        int? statusCode = null;

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync($"orders/{orderId}", cancellationToken);
            statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                state.RecordSuccess(orderId, statusCode.Value, observedAt);
                RequestSucceeded(logger, orderId, statusCode.Value, stopwatch.Elapsed.TotalMilliseconds);
            }
            else
            {
                state.RecordFailure(orderId, statusCode, observedAt, $"HTTP {statusCode}");
                RequestFailed(logger, orderId, statusCode.Value, stopwatch.Elapsed.TotalMilliseconds);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            state.RecordFailure(orderId, statusCode, observedAt, exception.Message);
            RequestThrew(logger, exception, orderId, stopwatch.Elapsed.TotalMilliseconds);
        }
        finally
        {
            telemetry.RecordRequest(
                statusCode,
                stopwatch.Elapsed.TotalMilliseconds,
                statusCode is null or < 200 or >= 300);
        }
    }

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "Telemetry generator started for {TargetBaseUrl} at {RequestsPerSecond} requests per second")]
    private static partial void GeneratorStarted(
        ILogger logger,
        string targetBaseUrl,
        int requestsPerSecond);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Debug,
        Message = "Generated request for order {OrderId} returned HTTP {StatusCode} in {DurationMilliseconds}ms")]
    private static partial void RequestSucceeded(
        ILogger logger,
        long orderId,
        int statusCode,
        double durationMilliseconds);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Warning,
        Message = "Generated request for order {OrderId} returned HTTP {StatusCode} in {DurationMilliseconds}ms")]
    private static partial void RequestFailed(
        ILogger logger,
        long orderId,
        int statusCode,
        double durationMilliseconds);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Error,
        Message = "Generated request for order {OrderId} failed after {DurationMilliseconds}ms")]
    private static partial void RequestThrew(
        ILogger logger,
        Exception exception,
        long orderId,
        double durationMilliseconds);
}
