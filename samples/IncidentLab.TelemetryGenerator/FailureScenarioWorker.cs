using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace IncidentLab.TelemetryGenerator;

public sealed partial class FailureScenarioWorker(
    IHttpClientFactory httpClientFactory,
    IOptions<TelemetryGeneratorOptions> options,
    TimeProvider timeProvider,
    ILogger<FailureScenarioWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.AutomatedFailuresEnabled)
        {
            AutomationDisabled(logger);
            return;
        }

        if (settings.InitialHealthyDelaySeconds > 0)
            await Task.Delay(
                TimeSpan.FromSeconds(settings.InitialHealthyDelaySeconds), timeProvider, stoppingToken);

        var scenarios = settings.FailureScenarioIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (scenarios.Length == 0)
            throw new InvalidOperationException("At least one automated failure scenario is required.");

        var scenarioIndex = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            var scenario = scenarios[scenarioIndex];
            await StartScenarioAsync(settings, scenario, stoppingToken);
            await Task.Delay(
                TimeSpan.FromSeconds(settings.FailureDurationSeconds), timeProvider, stoppingToken);
            await StopScenarioAsync(stoppingToken);
            RecoveryStarted(logger, settings.HealthyDurationSeconds);
            await Task.Delay(
                TimeSpan.FromSeconds(settings.HealthyDurationSeconds), timeProvider, stoppingToken);
            scenarioIndex = (scenarioIndex + 1) % scenarios.Length;
        }
    }

    private async Task StartScenarioAsync(
        TelemetryGeneratorOptions settings,
        string scenario,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(OrderTrafficWorker.HttpClientName);
        using var response = await client.PostAsJsonAsync(
            $"scenarios/{Uri.EscapeDataString(scenario)}/start",
            new
            {
                durationSeconds = settings.FailureDurationSeconds,
                delayMilliseconds = settings.FailureDelayMilliseconds
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        ScenarioStarted(
            logger, scenario,
            settings.FailureDurationSeconds, settings.FailureDelayMilliseconds);
    }

    private async Task StopScenarioAsync(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(OrderTrafficWorker.HttpClientName);
        using var response = await client.PostAsync("scenarios/stop", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    [LoggerMessage(2100, LogLevel.Information, "Automated failure scenarios are disabled")]
    private static partial void AutomationDisabled(ILogger logger);

    [LoggerMessage(2101, LogLevel.Warning,
        "Automated failure scenario started Scenario={Scenario} DurationSeconds={DurationSeconds} DelayMilliseconds={DelayMilliseconds}")]
    private static partial void ScenarioStarted(
        ILogger logger, string scenario, int durationSeconds, int? delayMilliseconds);

    [LoggerMessage(2102, LogLevel.Information,
        "Automated failure recovery window started HealthyDurationSeconds={HealthyDurationSeconds}")]
    private static partial void RecoveryStarted(ILogger logger, int healthyDurationSeconds);
}
