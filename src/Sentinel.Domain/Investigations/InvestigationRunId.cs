namespace Sentinel.Domain.Investigations;

public readonly record struct InvestigationRunId
{
    public InvestigationRunId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("An investigation run ID cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
    public static InvestigationRunId New() => new(Guid.NewGuid());
}
