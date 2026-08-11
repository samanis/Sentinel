using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Sentinel.Application.AI;

namespace Sentinel.Infrastructure.AI;

public sealed class OllamaEmbeddingClient(
    HttpClient httpClient,
    IOptions<EmbeddingOptions> options) : IEmbeddingClient
{
    public async Task<EmbeddingResult> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Embedding text is required.", nameof(text));
        var settings = options.Value;
        if (settings.Dimensions != EmbeddingOptions.RequiredDimensions)
            throw new InvalidOperationException(
                $"The configured embedding dimension must be {EmbeddingOptions.RequiredDimensions}.");

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsJsonAsync("api/embed", new
            {
                model = settings.Model,
                input = text,
                truncate = true
            }, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new EmbeddingException("The Ollama embedding request failed.", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new EmbeddingException(
                    $"Ollama returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
            var payload = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(
                cancellationToken: cancellationToken);
            var vector = payload?.Embeddings?.SingleOrDefault();
            if (vector is null || vector.Length != settings.Dimensions)
                throw new EmbeddingException(
                    $"Ollama must return exactly {settings.Dimensions} embedding dimensions.");
            if (vector.Any(value => !float.IsFinite(value)))
                throw new EmbeddingException("Ollama returned a non-finite embedding value.");
            return new EmbeddingResult(
                string.IsNullOrWhiteSpace(payload!.Model) ? settings.Model : payload.Model,
                vector);
        }
    }

    private sealed record OllamaEmbeddingResponse(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("embeddings")] float[][]? Embeddings);
}

public sealed class EmbeddingException(string message, Exception? innerException = null)
    : Exception(message, innerException);
