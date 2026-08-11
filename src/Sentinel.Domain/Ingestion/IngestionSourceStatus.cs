namespace Sentinel.Domain.Ingestion;

public enum IngestionSourceStatus
{
    Pending = 1,
    Succeeded = 2,
    Failed = 3,
    Skipped = 4
}
