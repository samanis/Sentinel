namespace Sentinel.Domain.Ingestion;

public readonly record struct IngestionRunId
{
    public IngestionRunId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("An ingestion run ID cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
    public static IngestionRunId New() => new(Guid.NewGuid());
}
