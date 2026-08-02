using System.Diagnostics;
using System.Diagnostics.Metrics;
using IncidentLab.OrderApi.Scenarios;

namespace IncidentLab.OrderApi.Telemetry;

public sealed class IncidentLabTelemetry
{
    public const string ActivitySourceName = "IncidentLab.OrderApi";
    public const string MeterName = "IncidentLab.OrderApi";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private readonly Counter<long> orderRequests;
    private readonly Counter<long> orderFailures;
    private readonly Histogram<double> orderDuration;

    public IncidentLabTelemetry(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        orderRequests = meter.CreateCounter<long>("incidentlab.order.requests");
        orderFailures = meter.CreateCounter<long>("incidentlab.order.failures");
        orderDuration = meter.CreateHistogram<double>("incidentlab.order.duration", "ms");
    }

    public void RecordRequest(ScenarioKind scenario) =>
        orderRequests.Add(1, new KeyValuePair<string, object?>("incidentlab.scenario", scenario.ToString()));

    public void RecordFailure(ScenarioKind scenario) =>
        orderFailures.Add(1, new KeyValuePair<string, object?>("incidentlab.scenario", scenario.ToString()));

    public void RecordDuration(double milliseconds, ScenarioKind scenario) =>
        orderDuration.Record(
            milliseconds,
            new KeyValuePair<string, object?>("incidentlab.scenario", scenario.ToString()));
}
