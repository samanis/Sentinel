namespace Sentinel.Domain.Incidents;

public sealed class Incident
{
    public const int MaxTitleLength = 200;
    public const int MaxServiceLength = 100;

    private Incident(
        IncidentId id,
        string title,
        string service,
        DateTimeOffset startedAt,
        IncidentSeverity severity,
        DateTimeOffset createdAt)
    {
        Id = id;
        Title = title;
        Service = service;
        StartedAt = startedAt;
        Severity = severity;
        Status = IncidentStatus.Open;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public IncidentId Id { get; }

    public string Title { get; }

    public string Service { get; }

    public DateTimeOffset StartedAt { get; }

    public IncidentSeverity Severity { get; }

    public IncidentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public static Incident Create(
        string title,
        string service,
        DateTimeOffset startedAt,
        IncidentSeverity severity,
        DateTimeOffset createdAt)
    {
        var normalizedTitle = ValidateText(title, MaxTitleLength, nameof(title));
        var normalizedService = ValidateText(service, MaxServiceLength, nameof(service));

        if (startedAt == default)
        {
            throw new ArgumentException("The incident start time is required.", nameof(startedAt));
        }

        if (createdAt == default)
        {
            throw new ArgumentException("The incident creation time is required.", nameof(createdAt));
        }

        if (createdAt < startedAt)
        {
            throw new ArgumentException(
                "The incident creation time cannot precede its start time.",
                nameof(createdAt));
        }

        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unsupported incident severity.");
        }

        return new Incident(
            IncidentId.New(),
            normalizedTitle,
            normalizedService,
            startedAt,
            severity,
            createdAt);
    }

    public void StartInvestigation(DateTimeOffset occurredAt)
    {
        EnsureStatus(IncidentStatus.Open, IncidentStatus.Investigating);
        ApplyStatus(IncidentStatus.Investigating, occurredAt);
    }

    public void Resolve(DateTimeOffset occurredAt)
    {
        if (Status is not (IncidentStatus.Open or IncidentStatus.Investigating))
        {
            throw InvalidTransition(IncidentStatus.Resolved);
        }

        ApplyStatus(IncidentStatus.Resolved, occurredAt);
        ResolvedAt = occurredAt;
    }

    public void Close(DateTimeOffset occurredAt)
    {
        EnsureStatus(IncidentStatus.Resolved, IncidentStatus.Closed);
        ApplyStatus(IncidentStatus.Closed, occurredAt);
        ClosedAt = occurredAt;
    }

    private static string ValidateText(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"The value cannot exceed {maxLength} characters.",
                parameterName);
        }

        return normalized;
    }

    private void EnsureStatus(IncidentStatus expected, IncidentStatus target)
    {
        if (Status != expected)
        {
            throw InvalidTransition(target);
        }
    }

    private void ApplyStatus(IncidentStatus status, DateTimeOffset occurredAt)
    {
        if (occurredAt < UpdatedAt)
        {
            throw new IncidentDomainException(
                "An incident lifecycle change cannot occur before the previous change.");
        }

        Status = status;
        UpdatedAt = occurredAt;
    }

    private IncidentDomainException InvalidTransition(IncidentStatus target) =>
        new($"An incident cannot transition from {Status} to {target}.");
}
