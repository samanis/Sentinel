namespace Sentinel.Application.Evidence.MetricIngestion;

public sealed class MetricSourceException(
    string failureCategory,
    string message,
    string? payloadHash = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string FailureCategory { get; } = failureCategory;
    public string? PayloadHash { get; } = payloadHash;
}
