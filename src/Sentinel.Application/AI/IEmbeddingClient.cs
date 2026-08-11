namespace Sentinel.Application.AI;

public sealed record EmbeddingResult(string Model, float[] Vector);

public interface IEmbeddingClient
{
    Task<EmbeddingResult> EmbedAsync(string text, CancellationToken cancellationToken);
}
