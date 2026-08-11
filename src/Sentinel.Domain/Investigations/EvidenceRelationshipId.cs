namespace Sentinel.Domain.Investigations;

public readonly record struct EvidenceRelationshipId
{
    public EvidenceRelationshipId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("An Evidence relationship ID cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
    public static EvidenceRelationshipId New() => new(Guid.NewGuid());
}
