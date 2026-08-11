namespace IncidentLab.TelemetryGenerator;

public sealed record GeneratorSnapshot(
    bool IsRunning,
    long RequestCount,
    long FailureCount,
    long? LastOrderId,
    int? LastStatusCode,
    DateTimeOffset? LastRequestAt,
    string? LastError);

public sealed class GeneratorState
{
    private readonly Lock sync = new();
    private bool isRunning;
    private long requestCount;
    private long failureCount;
    private long? lastOrderId;
    private int? lastStatusCode;
    private DateTimeOffset? lastRequestAt;
    private string? lastError;

    public void MarkRunning()
    {
        lock (sync)
        {
            isRunning = true;
        }
    }

    public void RecordSuccess(long orderId, int statusCode, DateTimeOffset observedAt)
    {
        lock (sync)
        {
            requestCount++;
            lastOrderId = orderId;
            lastStatusCode = statusCode;
            lastRequestAt = observedAt;
            lastError = null;
        }
    }

    public void RecordFailure(
        long orderId,
        int? statusCode,
        DateTimeOffset observedAt,
        string? error)
    {
        lock (sync)
        {
            requestCount++;
            failureCount++;
            lastOrderId = orderId;
            lastStatusCode = statusCode;
            lastRequestAt = observedAt;
            lastError = error;
        }
    }

    public GeneratorSnapshot GetSnapshot()
    {
        lock (sync)
        {
            return new GeneratorSnapshot(
                isRunning,
                requestCount,
                failureCount,
                lastOrderId,
                lastStatusCode,
                lastRequestAt,
                lastError);
        }
    }
}
