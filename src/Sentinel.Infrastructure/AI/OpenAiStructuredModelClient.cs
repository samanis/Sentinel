using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sentinel.Application.AI;

namespace Sentinel.Infrastructure.AI;

public sealed class OpenAiStructuredModelClient(
    HttpClient httpClient,
    IOptions<OpenAiModelOptions> options) : IStructuredModelClient
{
    public async Task<StructuredModelResponse> GenerateAsync(
        StructuredModelRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new StructuredModelException(
                "The configured model provider is unavailable. Set OpenAI:ApiKey or OPENAI__APIKEY.");

        using var apiRequest = new HttpRequestMessage(HttpMethod.Post, "responses");
        apiRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        apiRequest.Content = JsonContent.Create(new
        {
            model = settings.Model,
            store = false,
            max_output_tokens = request.MaxOutputTokens,
            reasoning = new { effort = "medium" },
            instructions = request.Instructions,
            input = request.Input,
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = request.OutputName,
                    strict = true,
                    schema = request.OutputSchema
                }
            }
        });

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(apiRequest, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new StructuredModelException("The model provider request failed.", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new StructuredModelException(
                    $"The model provider returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            var status = root.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString()
                : null;
            if (!string.Equals(status, "completed", StringComparison.Ordinal))
                throw new StructuredModelException(
                    $"The model provider response was not completed (status: {status ?? "unknown"}).");

            var outputText = FindOutputText(root)
                ?? throw new StructuredModelException("The model provider response did not contain structured output.");
            var model = root.TryGetProperty("model", out var modelElement)
                ? modelElement.GetString()
                : null;
            return new StructuredModelResponse(
                string.IsNullOrWhiteSpace(model) ? settings.Model : model,
                outputText);
        }
    }

    private static string? FindOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("type", out var itemType) || itemType.GetString() != "message" ||
                !item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var partType) && partType.GetString() == "output_text" &&
                    part.TryGetProperty("text", out var text))
                    return text.GetString();
                if (part.TryGetProperty("type", out partType) && partType.GetString() == "refusal")
                    throw new StructuredModelException("The model provider refused the generation request.");
            }
        }
        return null;
    }
}
