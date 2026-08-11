using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Sentinel.Application.Evidence.LogIngestion;

namespace Sentinel.Infrastructure.Observability;

public sealed class LokiLogSource(HttpClient httpClient) : ILogSource
{
    public async Task<IReadOnlyList<LogObservation>> QueryAsync(
        LogQuery query,
        CancellationToken cancellationToken)
    {
        var selector = $"{{service_name=\"{EscapeLogQl(query.ServiceName)}\"}}";
        var uri = $"loki/api/v1/query_range?query={Uri.EscapeDataString(selector)}" +
            $"&start={ToNanoseconds(query.From)}&end={ToNanoseconds(query.To)}" +
            $"&limit={query.Limit}&direction=backward";
        string? payloadHash = null;

        try
        {
            using var response = await httpClient.GetAsync(uri, cancellationToken);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            payloadHash = Convert.ToHexString(SHA256.HashData(payload));
            using var document = JsonDocument.Parse(payload);
            return Parse(document.RootElement);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LogSourceException("Timeout", "Loki did not respond before the request timeout.");
        }
        catch (HttpRequestException exception)
        {
            throw new LogSourceException("SourceUnavailable", "Loki log query failed.", innerException: exception);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or OverflowException)
        {
            throw new LogSourceException("InvalidPayload", "Loki returned an invalid log payload.", payloadHash, exception);
        }
    }

    private static List<LogObservation> Parse(JsonElement root)
    {
        var result = root.GetProperty("data").GetProperty("result");
        var observations = new List<LogObservation>();
        foreach (var streamResult in result.EnumerateArray())
        {
            var streamAttributes = ReadObject(streamResult.GetProperty("stream"));
            foreach (var value in streamResult.GetProperty("values").EnumerateArray())
            {
                var parts = value.EnumerateArray().ToArray();
                if (parts.Length < 2) throw new JsonException("A Loki value must contain a timestamp and body.");
                var timestamp = parts[0].GetString() ?? throw new JsonException("Missing Loki timestamp.");
                var attributes = new Dictionary<string, string>(
                    streamAttributes, StringComparer.OrdinalIgnoreCase);
                var metadata = parts.Length > 2 && parts[2].ValueKind == JsonValueKind.Object
                    ? ReadObject(parts[2]) : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in metadata) attributes[pair.Key] = pair.Value;
                observations.Add(new LogObservation(
                    timestamp,
                    FromNanoseconds(timestamp),
                    Get(attributes, "service_name") ?? "unknown-service",
                    Get(attributes, "severity_text") ?? Get(attributes, "level") ?? "UNSPECIFIED",
                    parts[1].GetString() ?? string.Empty,
                    Get(attributes, "trace_id"),
                    Get(attributes, "span_id"),
                    new Dictionary<string, string>(attributes)));
            }
        }

        return observations;
    }

    private static Dictionary<string, string> ReadObject(JsonElement element) =>
        element.EnumerateObject().ToDictionary(
            property => property.Name,
            property => property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString() ?? string.Empty
                : property.Value.GetRawText(),
            StringComparer.OrdinalIgnoreCase);

    private static string? Get(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static string EscapeLogQl(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static long ToNanoseconds(DateTimeOffset value) =>
        checked(value.ToUnixTimeMilliseconds() * 1_000_000 + value.Ticks % TimeSpan.TicksPerMillisecond * 100);

    private static DateTimeOffset FromNanoseconds(string value)
    {
        var nanoseconds = long.Parse(value, CultureInfo.InvariantCulture);
        var seconds = Math.DivRem(nanoseconds, 1_000_000_000L, out var remainder);
        return DateTimeOffset.FromUnixTimeSeconds(seconds).AddTicks(remainder / 100);
    }
}
