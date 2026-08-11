using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sentinel.Application.Abstractions;
using Sentinel.Domain.Ingestion;

namespace Sentinel.Application.Ingestion;

public sealed record PrometheusAlertInput(
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyDictionary<string, string>? Annotations,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    string? GeneratorUrl);

public sealed record AcceptPrometheusAlertsResult(
    int NotificationCount,
    int CreatedCount,
    int DuplicateCount,
    IReadOnlyList<AcceptedIngestion> Ingestions);

public sealed class AcceptPrometheusAlertsUseCase(
    IAlertIngestionRepository repository,
    IClock clock)
{
    public const int MaximumBatchSize = 100;
    public const int MaximumLabelsPerAlert = 100;
    public const int MaximumAnnotationsPerAlert = 100;
    public const int MaximumLabelNameLength = 200;
    public const int MaximumLabelValueLength = 2_000;

    public async Task<AcceptPrometheusAlertsResult> ExecuteAsync(
        IReadOnlyCollection<PrometheusAlertInput> inputs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.Count is < 1 or > MaximumBatchSize)
            throw new ArgumentException($"An alert batch must contain between 1 and {MaximumBatchSize} alerts.", nameof(inputs));

        var receivedAt = clock.UtcNow;
        var alerts = inputs.Select(input => CreateAlert(input, receivedAt)).ToArray();
        var accepted = await repository.AcceptAsync(alerts, receivedAt, cancellationToken);
        var createdCount = accepted.Count(item => item.WasCreated);

        return new AcceptPrometheusAlertsResult(
            inputs.Count,
            createdCount,
            inputs.Count - createdCount,
            accepted);
    }

    private static AlertOccurrence CreateAlert(PrometheusAlertInput input, DateTimeOffset receivedAt)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateMap(input.Labels, MaximumLabelsPerAlert, "labels");
        ValidateMap(input.Annotations ?? new Dictionary<string, string>(), MaximumAnnotationsPerAlert, "annotations");

        if (!input.Labels.TryGetValue("alertname", out var alertName) || string.IsNullOrWhiteSpace(alertName))
            throw new ArgumentException("Every alert must contain a non-empty 'alertname' label.", nameof(input));
        if (!input.Labels.TryGetValue("service", out var service) || string.IsNullOrWhiteSpace(service))
            throw new ArgumentException("Every alert must contain a non-empty 'service' label.", nameof(input));

        var environment = input.Labels.TryGetValue("environment", out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : "unknown";
        var startsAt = input.StartsAt ?? receivedAt;
        if (startsAt > receivedAt.AddMinutes(5))
            throw new ArgumentException("An alert start time cannot be more than five minutes in the future.", nameof(input));

        var labels = new SortedDictionary<string, string>(
            input.Labels.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            StringComparer.Ordinal);
        var annotations = new SortedDictionary<string, string>(
            (input.Annotations ?? new Dictionary<string, string>())
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            StringComparer.Ordinal);
        var labelsJson = JsonSerializer.Serialize(labels);
        var annotationsJson = JsonSerializer.Serialize(annotations);
        var occurrenceKey = CreateOccurrenceKey(labelsJson, startsAt);

        var endsAt = input.EndsAt is { } endValue && endValue == default(DateTimeOffset)
            ? null
            : input.EndsAt;
        return AlertOccurrence.Create(
            occurrenceKey,
            alertName,
            service,
            environment,
            startsAt,
            endsAt,
            receivedAt,
            labelsJson,
            annotationsJson,
            input.GeneratorUrl);
    }

    private static void ValidateMap(
        IReadOnlyDictionary<string, string> values,
        int maximumCount,
        string fieldName)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count > maximumCount)
            throw new ArgumentException($"An alert cannot contain more than {maximumCount} {fieldName}.", fieldName);
        foreach (var pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > MaximumLabelNameLength)
                throw new ArgumentException($"Every {fieldName} name must contain 1 through {MaximumLabelNameLength} characters.", fieldName);
            if (pair.Value is null || pair.Value.Length > MaximumLabelValueLength)
                throw new ArgumentException($"Every {fieldName} value must contain at most {MaximumLabelValueLength} characters.", fieldName);
        }
    }

    private static string CreateOccurrenceKey(string labelsJson, DateTimeOffset startsAt)
    {
        var value = $"{labelsJson}\n{startsAt.ToUniversalTime():O}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
