namespace Sentinel.Domain.Ingestion;

public readonly record struct AlertOccurrenceId
{
    public AlertOccurrenceId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("An alert occurrence ID cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
    public static AlertOccurrenceId New() => new(Guid.NewGuid());
}
