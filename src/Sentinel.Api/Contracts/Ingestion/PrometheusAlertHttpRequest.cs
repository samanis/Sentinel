namespace Sentinel.Api.Contracts.Ingestion;

public sealed record AlertmanagerWebhookHttpRequest(
    string? Receiver,
    string? Status,
    IReadOnlyList<PrometheusAlertHttpRequest> Alerts);

public sealed record PrometheusAlertHttpRequest(
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyDictionary<string, string>? Annotations,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    string? GeneratorUrl);
