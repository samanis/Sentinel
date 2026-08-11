namespace Sentinel.Domain.Evidence;

public readonly record struct EvidenceId
{
    public EvidenceId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An evidence ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static EvidenceId New() => new(Guid.NewGuid());
}
