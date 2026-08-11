using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Sentinel.Application.Evidence.TraceIngestion;

namespace Sentinel.Infrastructure.Observability;

public sealed class TempoTraceSource(HttpClient httpClient) : ITraceSource
{
    public async Task<TraceObservation?> GetTraceAsync(
        string traceId,
        CancellationToken cancellationToken)
    {
        string? payloadHash = null;
        try
        {
            using var response = await httpClient.GetAsync(
                $"api/traces/{Uri.EscapeDataString(traceId)}",
                cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            payloadHash = Convert.ToHexString(SHA256.HashData(payload));
            using var document = JsonDocument.Parse(payload);
            return Parse(traceId, document.RootElement);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TraceSourceException(
                "Timeout",
                "Tempo did not respond before the request timeout.");
        }
        catch (HttpRequestException exception)
        {
            throw new TraceSourceException(
                "SourceUnavailable",
                "Tempo trace retrieval failed.",
                innerException: exception);
        }
        catch (JsonException exception)
        {
            throw new TraceSourceException(
                "MalformedJson",
                "Tempo returned malformed trace JSON.",
                payloadHash: payloadHash,
                innerException: exception);
        }
        catch (TraceValidationException exception)
        {
            throw new TraceSourceException(
                exception.FailureCategory,
                "Tempo returned an invalid trace payload.",
                exception.InvalidField,
                payloadHash,
                exception);
        }
    }

    private static TraceObservation Parse(string traceId, JsonElement root)
    {
        if (!root.TryGetProperty("batches", out var batches) ||
            batches.ValueKind != JsonValueKind.Array)
        {
            throw new TraceValidationException(
                "MissingRequiredField",
                "The trace payload does not contain a batches array.",
                "batches");
        }

        var spans = new List<TraceSpanObservation>();
        foreach (var batch in batches.EnumerateArray())
        {
            var serviceName = ReadServiceName(batch);
            if (!batch.TryGetProperty("scopeSpans", out var scopeSpans) ||
                scopeSpans.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var scopeSpan in scopeSpans.EnumerateArray())
            {
                if (!scopeSpan.TryGetProperty("spans", out var batchSpans) ||
                    batchSpans.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var span in batchSpans.EnumerateArray())
                {
                    spans.Add(ParseSpan(serviceName, span));
                }
            }
        }

        if (spans.Count == 0)
        {
            throw new TraceValidationException(
                "NoSpans",
                "The trace payload does not contain any spans.");
        }

        return new TraceObservation(traceId, spans);
    }

    private static TraceSpanObservation ParseSpan(string serviceName, JsonElement span)
    {
        var spanId = RequiredString(span, "spanId");
        var name = RequiredString(span, "name");
        var startNanoseconds = RequiredString(span, "startTimeUnixNano");
        if (!long.TryParse(startNanoseconds, CultureInfo.InvariantCulture, out var nanoseconds))
        {
            throw new TraceValidationException(
                "InvalidTimestamp",
                "A trace span has an invalid start timestamp.",
                "startTimeUnixNano");
        }

        var attributes = ReadAttributes(span);
        var events = ReadEvents(span);
        var isError = false;
        string? statusMessage = null;
        if (span.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.Object)
        {
            isError = status.TryGetProperty("code", out var code) &&
                string.Equals(code.GetString(), "STATUS_CODE_ERROR", StringComparison.Ordinal);
            statusMessage = status.TryGetProperty("message", out var message)
                ? message.GetString()
                : null;
        }

        var seconds = Math.DivRem(nanoseconds, 1_000_000_000L, out var remainder);
        var startedAt = DateTimeOffset.FromUnixTimeSeconds(seconds).AddTicks(remainder / 100);

        return new TraceSpanObservation(
            spanId,
            serviceName,
            name,
            startedAt,
            isError,
            statusMessage,
            attributes,
            events);
    }

    private static string ReadServiceName(JsonElement batch)
    {
        if (!batch.TryGetProperty("resource", out var resource))
        {
            return "unknown-service";
        }

        var attributes = ReadAttributes(resource);
        return attributes.TryGetValue("service.name", out var serviceName)
            ? serviceName
            : "unknown-service";
    }

    private static Dictionary<string, string> ReadAttributes(JsonElement owner)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!owner.TryGetProperty("attributes", out var attributes) ||
            attributes.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var attribute in attributes.EnumerateArray())
        {
            if (!attribute.TryGetProperty("key", out var keyElement) ||
                !attribute.TryGetProperty("value", out var valueElement))
            {
                continue;
            }

            var key = keyElement.GetString();
            var value = ReadAttributeValue(valueElement);
            if (!string.IsNullOrWhiteSpace(key) && value is not null)
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static string? ReadAttributeValue(JsonElement value) =>
        value.EnumerateObject()
            .Select(property => property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetRawText(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => null
            })
            .FirstOrDefault(candidate => candidate is not null);

    private static string[] ReadEvents(JsonElement span)
    {
        if (!span.TryGetProperty("events", out var events) ||
            events.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return events.EnumerateArray()
            .Where(item => item.TryGetProperty("name", out _))
            .Select(item => item.GetProperty("name").GetString())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToArray();
    }

    private static string RequiredString(JsonElement owner, string propertyName)
    {
        if (!owner.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new TraceValidationException(
                "MissingRequiredField",
                $"A trace span is missing '{propertyName}'.",
                propertyName);
        }

        return property.GetString()!;
    }

    private sealed class TraceValidationException(
        string failureCategory,
        string message,
        string? invalidField = null) : Exception(message)
    {
        public string FailureCategory { get; } = failureCategory;

        public string? InvalidField { get; } = invalidField;
    }
}
