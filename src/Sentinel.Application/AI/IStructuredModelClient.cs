using System.Text.Json;

namespace Sentinel.Application.AI;

public interface IStructuredModelClient
{
    Task<StructuredModelResponse> GenerateAsync(
        StructuredModelRequest request,
        CancellationToken cancellationToken);
}

public sealed record StructuredModelRequest(
    string Instructions,
    string Input,
    string OutputName,
    JsonElement OutputSchema,
    int MaxOutputTokens);

public sealed record StructuredModelResponse(
    string Model,
    string Output);

public sealed class StructuredModelException : Exception
{
    public StructuredModelException(string message) : base(message) { }

    public StructuredModelException(string message, Exception innerException)
        : base(message, innerException) { }
}
