# Incident Lab Order API

This independently runnable sample represents an external system monitored by
Sentinel. It intentionally has no project reference to Sentinel. A controlled
fault produces repeatable logs, traces, metrics, latency, and HTTP errors for
the investigation pipeline to collect later.

Run it locally:

```powershell
dotnet run --project samples/IncidentLab.OrderApi
```

List the available scenarios:

```powershell
Invoke-RestMethod http://localhost:5112/scenarios
```

Start a scenario and generate an affected request:

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5112/scenarios/slow-database/start `
  -ContentType application/json `
  -Body '{"durationSeconds":60,"delayMilliseconds":1500}'

Invoke-RestMethod http://localhost:5112/orders/42

Invoke-RestMethod -Method Post http://localhost:5112/scenarios/stop
```

Available scenarios:

| ID | Behavior |
| --- | --- |
| `slow-database` | Returns HTTP 504 with a sanitized slow SQL query and database span attributes. |
| `database-unavailable` | Returns HTTP 503 after a configurable delay. |
| `dependency-timeout` | Returns HTTP 504 after a configurable delay. |
| `external-api-timeout` | Returns HTTP 504 for a timed-out payment REST API call. |
| `web-service-unavailable` | Returns HTTP 502 for an unavailable SOAP inventory service. |
| `ftp-transfer-failure` | Returns HTTP 502 for a failed partner FTP upload. |
| `memory-leak` | Returns HTTP 503 while retaining memory up to a safe 32 MiB cap. |
| `unhandled-exception` | Returns HTTP 500 with exception telemetry. |

Only one scenario is active at a time. It stops automatically when its duration
expires, or immediately through `POST /scenarios/stop`. Every order request is
tagged with `incidentlab.scenario` in custom traces and metrics. Failure
responses include the scenario and trace ID.

The automated Telemetry Generator rotates through `slow-database`,
`external-api-timeout`, `web-service-unavailable`, `ftp-transfer-failure`, and
`memory-leak`. Logs include the dependency type, operation, target, sanitized
diagnostic statement, delay, and HTTP status. Trace spans carry matching
database, HTTP, RPC, FTP, or process-memory attributes. The memory allocation is
bounded and cleared when a scenario is stopped or changed.

The sample can later move to its own repository without changing Sentinel's
production projects. Its current location keeps the portfolio demo easy to
clone and run while the two applications retain separate process and container
boundaries.
