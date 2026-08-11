namespace Sentinel.Infrastructure.Persistence;

public sealed class AlertNotificationRecord
{
    private AlertNotificationRecord() { }

    public Guid Id { get; private set; }
    public string OccurrenceKey { get; private set; } = string.Empty;
    public string AlertName { get; private set; } = string.Empty;
    public string Service { get; private set; } = string.Empty;
    public string Environment { get; private set; } = string.Empty;
    public string LabelsJson { get; private set; } = "{}";
    public string AnnotationsJson { get; private set; } = "{}";
    public DateTimeOffset ReceivedAt { get; private set; }

    public static AlertNotificationRecord Create(
        string occurrenceKey, string alertName, string service, string environment,
        string labelsJson, string annotationsJson, DateTimeOffset receivedAt) => new()
        {
            Id = Guid.NewGuid(),
            OccurrenceKey = occurrenceKey,
            AlertName = alertName,
            Service = service,
            Environment = environment,
            LabelsJson = labelsJson,
            AnnotationsJson = annotationsJson,
            ReceivedAt = receivedAt
        };
}
