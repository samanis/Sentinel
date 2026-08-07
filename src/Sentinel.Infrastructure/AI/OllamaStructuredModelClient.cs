using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sentinel.Application.AI;

namespace Sentinel.Infrastructure.AI;

public sealed class OllamaStructuredModelClient(
    HttpClient httpClient,
    IOptions<OllamaModelOptions> options) : IStructuredModelClient
{
    public async Task<StructuredModelResponse> GenerateAsync(
        StructuredModelRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = options.Value;
        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsJsonAsync("api/chat", new
            {
                model = settings.Model,
                stream = false,
                think = false,
                messages = new[]
                {
                    new { role = "system", content = request.Instructions },
                    new { role = "user", content = request.Input }
                },
                format = request.OutputSchema,
                options = new
                {
                    temperature = 0,
                    num_ctx = settings.ContextLength,
                    num_predict = Math.Min(request.MaxOutputTokens, settings.MaxOutputTokens)
                }
            }, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new StructuredModelException("The model provider request failed.", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new StructuredModelException(
                    $"The model provider returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (!root.TryGetProperty("done", out var done) || !done.GetBoolean())
                throw new StructuredModelException("The model provider response was incomplete.");
            if (!root.TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var content) ||
                string.IsNullOrWhiteSpace(content.GetString()))
                throw new StructuredModelException("The model provider response did not contain structured output.");
            var model = root.TryGetProperty("model", out var modelElement)
                ? modelElement.GetString()
                : null;
            return new StructuredModelResponse(
                string.IsNullOrWhiteSpace(model) ? settings.Model : model,
                content.GetString()!);
        }
    }
}
