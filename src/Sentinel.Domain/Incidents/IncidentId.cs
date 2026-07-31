namespace Sentinel.Domain.Incidents;

public readonly record struct IncidentId
{
    public IncidentId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An incident ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static IncidentId New() => new(Guid.NewGuid());

    public static IncidentId Parse(string value) => new(Guid.Parse(value));

    public override string ToString() => Value.ToString();
}
