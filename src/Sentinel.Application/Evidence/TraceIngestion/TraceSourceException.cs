namespace Sentinel.Application.Evidence.TraceIngestion;

public sealed class TraceSourceException : Exception
{
    public TraceSourceException(
        string failureCategory,
        string message,
        string? invalidField = null,
        string? payloadHash = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        FailureCategory = failureCategory;
        InvalidField = invalidField;
        PayloadHash = payloadHash;
    }

    public string FailureCategory { get; }

    public string? InvalidField { get; }

    public string? PayloadHash { get; }
}
