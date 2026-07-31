# Observability

Sentinel emits traces, metrics, and logs through the OpenTelemetry Protocol
(OTLP). Application code creates telemetry with the OpenTelemetry .NET SDK;
the Collector owns batching, retry, policy, and backend export.

## Local development

Start the shared local Collector:

```powershell
docker compose up -d otel-collector
```

Run either application normally:

```powershell
dotnet run --project src/Sentinel.Api
dotnet run --project samples/Sentinel.DemoService
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

The local Collector deliberately uses the debug exporter. A later milestone
will replace or supplement it with an observability backend.

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
- `sentinel-demo-service`

Both also emit their assembly version and deployment environment. Health-check
requests are excluded from tracing to reduce noise, while HTTP and runtime
metrics remain enabled.
