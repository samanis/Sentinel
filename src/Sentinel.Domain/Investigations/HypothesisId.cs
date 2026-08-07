namespace Sentinel.Domain.Investigations;

public readonly record struct HypothesisId
{
    public HypothesisId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("A hypothesis ID cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
    public static HypothesisId New() => new(Guid.NewGuid());
}
