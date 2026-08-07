using Sentinel.Domain.Incidents;

namespace Sentinel.Application.Evidence.TraceIngestion;

public sealed class DeterministicTraceEvidenceNormalizer : ITraceEvidenceNormalizer
{
    public IReadOnlyList<NormalizedTraceEvidence> Normalize(
        IncidentId incidentId,
        TraceObservation trace)
    {
        ArgumentNullException.ThrowIfNull(trace);

        return trace.Spans
            .Where(span => span.IsError)
            .Select(span => new NormalizedTraceEvidence(
                $"tempo://traces/{trace.TraceId}/spans/{Uri.EscapeDataString(span.SpanId)}",
                span.StartedAt,
                BuildSummary(span),
                trace.TraceId,
                span.SpanId,
                span.ServiceName))
            .ToArray();
    }

    private static string BuildSummary(TraceSpanObservation span)
    {
        var details = new List<string>
        {
            $"Service '{span.ServiceName}' reported an error in span '{span.Name}'."
        };

        if (!string.IsNullOrWhiteSpace(span.StatusMessage))
        {
            details.Add($"Status: {span.StatusMessage}.");
        }

        AddAttribute(details, span.Attributes, "http.response.status_code", "HTTP status");
        AddAttribute(details, span.Attributes, "incidentlab.scenario", "Scenario");

        if (span.Events.Count > 0)
        {
            details.Add($"Events: {string.Join(", ", span.Events)}.");
        }

        return string.Join(' ', details);
    }

    private static void AddAttribute(
        List<string> details,
        IReadOnlyDictionary<string, string> attributes,
        string key,
        string label)
    {
        if (attributes.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            details.Add($"{label}: {value}.");
        }
    }
}
