# Observability

Sentinel emits traces, metrics, and logs through the OpenTelemetry Protocol
(OTLP). Application code creates telemetry with the OpenTelemetry .NET SDK;
the Collector owns batching, retry, policy, and backend export. Traces are
persisted in Grafana Tempo, logs in Grafana Loki, and metrics in Prometheus.

## Grafana

The local Docker Compose environment runs Grafana at
`http://localhost:3000`. Sign in with the local-development defaults:

```text
Username: admin
Password: sentinel-local-dev-only
```

Override these defaults without editing source-controlled files:

```powershell
$env:GRAFANA_ADMIN_USER = "admin"
$env:GRAFANA_ADMIN_PASSWORD = "choose-a-local-password"
docker compose up -d grafana
```

Grafana provisions Prometheus, Loki, and Tempo from
`deploy/grafana/provisioning/datasources/datasources.yaml`. Prometheus is the
default data source. Tempo is linked to Loki for trace-to-log navigation and to
Prometheus for trace-to-metric navigation.

Grafana reads the telemetry backends; applications do not send telemetry to
Grafana. Both Sentinel and Grafana query Loki, Tempo, and Prometheus directly.

The provisioned **Incident Lab Overview** dashboard displays generator request
counts and rates, Incident Lab logs, and recent traces. Open it directly at:

```text
http://localhost:3000/d/incident-lab-overview/incident-lab-overview
```

## Local development

Start Tempo, Loki, Prometheus, Alertmanager, and the shared local Collector:

```powershell
docker compose up -d tempo loki prometheus alertmanager otel-collector
```

Prometheus loads alert rules from `deploy/prometheus/rules` and sends firing
alerts to Alertmanager at `alertmanager:9093`. Alertmanager uses the webhook
receiver in `deploy/alertmanager/alertmanager.yml` to call the Sentinel API at
`http://sentinel-api:8080/api/alerts/webhook`. The API acknowledges only after
durably creating the alert occurrence and pending ingestion run.

Run either application normally:

```powershell
dotnet run --project src/Sentinel.Api
dotnet run --project samples/IncidentLab.OrderApi
```

The OTLP exporter defaults to `http://localhost:4317`. Override it when
needed with the standard environment variable:

```powershell
$env:OTEL_EXPORTER_OTLP_ENDPOINT = "http://localhost:4317"
```

Generate traffic, then inspect the Collector's debug output:

```powershell
docker compose logs otel-collector
```

Tempo's query and readiness API is available at `http://localhost:3200`.
The Collector exports traces to both Tempo and its debug exporter. Tempo uses a
named Docker volume and retains local traces for 24 hours. The volume survives
`docker compose down`, but `docker compose down -v` deletes it.

Search recent traces with TraceQL:

```powershell
$query = [uri]::EscapeDataString('{ resource.service.name = "incidentlab-order-api" }')
Invoke-RestMethod "http://localhost:3200/api/search?q=$query"
```

## Sentinel Tempo connector

Sentinel reads Tempo through its HTTP query API. Tempo remains the source of
truth for raw traces; Sentinel validates the response and persists only
normalized error-span Evidence with trace and span provenance.

Import a known trace into an existing incident:

```powershell
$body = @{ traceId = "0123456789abcdef0123456789abcdef" } | ConvertTo-Json
Invoke-RestMethod `
  -Method Post `
  -Uri "http://localhost:5156/api/incidents/{incident-id}/evidence/tempo" `
  -ContentType "application/json" `
  -Body $body
```

The connector is read-only. Repeating an import returns the already-persisted
Evidence because its canonical content hash is unchanged. An unavailable or
malformed Tempo response is reported as an upstream failure and does not
modify the incident.

Each import emits structured lifecycle events:

- `TempoTraceImportStarted`
- `TempoTraceImportCompleted`
- `TempoTraceNotFound`
- `TempoTraceValidationFailed`
- `TempoTraceSourceUnavailable`

The events include incident and trace IDs plus applicable counts, duration,
failure category, invalid field, and payload hash. Sentinel never writes the
complete Tempo response to its logs. The payload hash identifies the rejected
response without retaining or exposing its contents.

The complete response is validated and normalized before persistence. All
selected error-span Evidence is saved through one EF Core `SaveChanges` call,
which provides an all-or-nothing database transaction for the import.

## Sentinel Loki connector

The Collector exports OTLP logs to `http://loki:3100/otlp`; it automatically
appends `/v1/logs`. Loki is available locally at `http://localhost:3100`, uses a
named volume, and retains logs for seven days.

Import warning and error logs for an incident service within a bounded range:

```powershell
$body = @{
  from = (Get-Date).ToUniversalTime().AddMinutes(-10).ToString("o")
  to = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json

Invoke-RestMethod `
  -Method Post `
  -Uri "http://localhost:5156/api/incidents/{incident-id}/evidence/loki" `
  -ContentType "application/json" `
  -Body $body
```

Sentinel uses Loki's `query_range` API with the incident's `service_name` and
the supplied range. The range cannot exceed 24 hours. Only WARN/WARNING,
ERROR, FATAL, and CRITICAL entries become Evidence; informational logs remain
in Loki. Trace and span IDs are read from structured metadata when available.
Repeating the same import is idempotent through the Evidence content hash.

## Sentinel Prometheus connector

The Collector sends metrics to Prometheus's remote-write receiver at
`/api/v1/write`. Prometheus is available locally at `http://localhost:9090`,
uses a named volume, and retains metrics for seven days.

```powershell
$body = @{
  from = (Get-Date).ToUniversalTime().AddMinutes(-10).ToString("o")
  to = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json

Invoke-RestMethod `
  -Method Post `
  -Uri "http://localhost:5156/api/incidents/{incident-id}/evidence/prometheus" `
  -ContentType "application/json" `
  -Body $body
```

The range must be between one minute and 24 hours. Sentinel queries cumulative
request count, cumulative failure count, and cumulative-histogram p95 latency
at the range end. These are truthful snapshots, not incident-window deltas:
the MVP does not yet capture a counter baseline before an incident begins.
Establishing that baseline belongs to the future Investigation Orchestrator.

## Deployment model

The intended production model is a Collector sidecar for each application
workload:

```text
Application -> OTLP on localhost -> Collector sidecar -> backend
```

In Kubernetes, the application container and Collector container will share a
pod and lifecycle. Collector configuration and backend credentials remain
outside the application. The application continues to use the same OTLP
exporter and standard environment variables.

## Resource identity

The applications currently emit these service names:

- `sentinel-api`
- `incidentlab-order-api`

Both also emit their assembly version and deployment environment. Health-check
requests are excluded from tracing to reduce noise, while HTTP and runtime
metrics remain enabled.
