using System.ComponentModel.DataAnnotations;

namespace IncidentLab.TelemetryGenerator;

public sealed class TelemetryGeneratorOptions
{
    public const string SectionName = "TelemetryGenerator";

    [Required]
    public string TargetBaseUrl { get; init; } = "http://localhost:5112";

    [Range(1, 20)]
    public int RequestsPerSecond { get; init; } = 1;

    [Range(1, 60)]
    public int RequestTimeoutSeconds { get; init; } = 10;

    [Range(1, long.MaxValue)]
    public long MinimumOrderId { get; init; } = 1;

    [Range(1, long.MaxValue)]
    public long MaximumOrderId { get; init; } = 10;

    public bool AutomatedFailuresEnabled { get; init; } = true;

    [Required]
    public string FailureScenarioIds { get; init; } =
        "slow-database,external-api-timeout,web-service-unavailable,ftp-transfer-failure,memory-leak";

    [Range(10, 300)]
    public int FailureDurationSeconds { get; init; } = 45;

    [Range(75, 600)]
    public int HealthyDurationSeconds { get; init; } = 90;

    [Range(0, 10_000)]
    public int? FailureDelayMilliseconds { get; init; }

    [Range(0, 300)]
    public int InitialHealthyDelaySeconds { get; init; } = 15;
}
