namespace Sentinel.Application.Evidence.LogIngestion;

public sealed class LogSourceException(
    string failureCategory,
    string message,
    string? payloadHash = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string FailureCategory { get; } = failureCategory;
    public string? PayloadHash { get; } = payloadHash;
}
