namespace Sentinel.Application.Evidence.TraceIngestion;

public interface ITraceSource
{
    Task<TraceObservation?> GetTraceAsync(
        string traceId,
        CancellationToken cancellationToken);
}
