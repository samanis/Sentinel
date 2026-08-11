using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace IncidentLab.TelemetryGenerator;

public sealed class TelemetryGeneratorTelemetry : IDisposable
{
    public const string MeterName = "IncidentLab.TelemetryGenerator";

    private readonly Meter meter = new(MeterName);
    private readonly Counter<long> requests;
    private readonly Counter<long> failures;
    private readonly Histogram<double> duration;

    public TelemetryGeneratorTelemetry()
    {
        requests = meter.CreateCounter<long>(
            "incidentlab.generator.requests",
            unit: "{request}",
            description: "Requests generated for the Incident Lab Order API.");
        failures = meter.CreateCounter<long>(
            "incidentlab.generator.failures",
            unit: "{request}",
            description: "Generated requests that failed or returned a non-success status.");
        duration = meter.CreateHistogram<double>(
            "incidentlab.generator.request.duration",
            unit: "ms",
            description: "Duration of generated Incident Lab requests.");
    }

    public void RecordRequest(int? statusCode, double durationMilliseconds, bool failed)
    {
        var tags = new TagList
        {
            { "http.response.status_code", statusCode ?? 0 },
            { "error.type", failed ? "request_failed" : string.Empty }
        };

        requests.Add(1, tags);
        duration.Record(durationMilliseconds, tags);
        if (failed)
        {
            failures.Add(1, tags);
        }
    }

    public void Dispose() => meter.Dispose();
}
