namespace Sentinel.Domain.Ingestion;

public enum IngestionRunStatus
{
    Pending,
    Running,
    Completed,
    Partial,
    Failed
}
