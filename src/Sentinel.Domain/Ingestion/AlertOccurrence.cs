namespace Sentinel.Domain.Ingestion;

public sealed class AlertOccurrence
{
    public const int OccurrenceKeyLength = 64;
    public const int MaxAlertNameLength = 200;
    public const int MaxServiceLength = 100;
    public const int MaxEnvironmentLength = 100;
    public const int MaxGeneratorUrlLength = 2_000;

    private AlertOccurrence(
        AlertOccurrenceId id,
        string occurrenceKey,
        string alertName,
        string service,
        string environment,
        DateTimeOffset startedAt,
        DateTimeOffset? endsAt,
        DateTimeOffset receivedAt,
        string labelsJson,
        string annotationsJson,
        string? generatorUrl)
    {
        Id = id;
        OccurrenceKey = occurrenceKey;
        AlertName = alertName;
        Service = service;
        Environment = environment;
        StartedAt = startedAt;
        EndsAt = endsAt;
        ReceivedAt = receivedAt;
        LabelsJson = labelsJson;
        AnnotationsJson = annotationsJson;
        GeneratorUrl = generatorUrl;
    }

    public AlertOccurrenceId Id { get; }
    public string OccurrenceKey { get; }
    public string AlertName { get; }
    public string Service { get; }
    public string Environment { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset? EndsAt { get; }
    public DateTimeOffset ReceivedAt { get; }
    public string LabelsJson { get; }
    public string AnnotationsJson { get; }
    public string? GeneratorUrl { get; }

    public static AlertOccurrence Create(
        string occurrenceKey,
        string alertName,
        string service,
        string environment,
        DateTimeOffset startedAt,
        DateTimeOffset? endsAt,
        DateTimeOffset receivedAt,
        string labelsJson,
        string annotationsJson,
        string? generatorUrl)
    {
        var normalizedKey = RequiredText(
            occurrenceKey,
            OccurrenceKeyLength,
            nameof(occurrenceKey));
        if (normalizedKey.Length != OccurrenceKeyLength)
            throw new ArgumentException("The occurrence key must be a SHA-256 hexadecimal value.", nameof(occurrenceKey));
        if (startedAt == default)
            throw new ArgumentException("The alert start time is required.", nameof(startedAt));
        if (receivedAt == default)
            throw new ArgumentException("The alert receipt time is required.", nameof(receivedAt));
        if (endsAt < startedAt)
            throw new ArgumentException("The alert end time cannot precede its start time.", nameof(endsAt));

        return new AlertOccurrence(
            AlertOccurrenceId.New(),
            normalizedKey.ToLowerInvariant(),
            RequiredText(alertName, MaxAlertNameLength, nameof(alertName)),
            RequiredText(service, MaxServiceLength, nameof(service)),
            RequiredText(environment, MaxEnvironmentLength, nameof(environment)),
            startedAt.ToUniversalTime(),
            endsAt?.ToUniversalTime(),
            receivedAt.ToUniversalTime(),
            RequiredText(labelsJson, int.MaxValue, nameof(labelsJson)),
            RequiredText(annotationsJson, int.MaxValue, nameof(annotationsJson)),
            OptionalText(generatorUrl, MaxGeneratorUrlLength, nameof(generatorUrl)));
    }

    private static string RequiredText(string value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A value is required.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"The value cannot exceed {maximumLength} characters.", parameterName);
        return normalized;
    }

    private static string? OptionalText(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"The value cannot exceed {maximumLength} characters.", parameterName);
        return normalized;
    }
}
