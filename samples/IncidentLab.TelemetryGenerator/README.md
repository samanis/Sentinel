# Incident Lab Telemetry Generator

The Telemetry Generator continuously calls the Incident Lab Order API so that
healthy and faulty scenarios produce a repeatable stream of logs, metrics, and
traces. It exercises the application over HTTP rather than fabricating OTLP
payloads.

Run it from the repository root after starting the Incident Lab and Collector:

```powershell
dotnet run --project samples/IncidentLab.TelemetryGenerator
```

The status API is available at `http://localhost:5113/status` when running with
Docker Compose. Configuration is under the `TelemetryGenerator` section:

| Setting | Default | Purpose |
| --- | ---: | --- |
| `TargetBaseUrl` | `http://localhost:5112/` | Incident Lab base URL |
| `RequestsPerSecond` | `1` | Deterministic request rate, from 1 through 20 |
| `RequestTimeoutSeconds` | `10` | Per-request timeout |
| `MinimumOrderId` | `1` | First generated order ID |
| `MaximumOrderId` | `10` | Last generated order ID before wrapping |
| `AutomatedFailuresEnabled` | `true` | Enables the recurring failure/recovery cycle |
| `FailureScenarioIds` | five rotating scenarios | Comma-separated failure scenario sequence |
| `FailureDurationSeconds` | `45` | Duration of each failure episode |
| `HealthyDurationSeconds` | `90` | Recovery time that allows the alert to resolve |

The default sequence is `slow-database`, `external-api-timeout`,
`web-service-unavailable`, `ftp-transfer-failure`, and `memory-leak`. After the
healthy recovery window, the generator activates the next cause automatically.
