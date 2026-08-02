# Day 1 Implementation Plan

## Objective

Build the Sentinel foundation and one observable demo service. The goal is a thin vertical slice with a reproducible failure, not a broad skeleton of unfinished abstractions.

AI and database persistence are deliberately excluded from this milestone.

## End-of-day outcome

The development environment should support this workflow:

1. Start the supporting containers.
2. Run the demo service.
3. Run the Sentinel API.
4. Call the healthy demo endpoint.
5. Enable a deterministic simulated fault.
6. Observe the resulting error or delay.
7. Create and retrieve an incident through the Sentinel API.
8. Confirm that both services emit structured logs and OpenTelemetry traces.

Expected commands:

```powershell
docker compose up -d
dotnet run --project samples/IncidentLab.OrderApi
dotnet run --project src/Sentinel.Api
```

## Initial solution structure

```text
Sentinel.sln
src/
  Sentinel.Api/
  Sentinel.Application/
  Sentinel.Domain/
  Sentinel.Infrastructure/
samples/
  IncidentLab.OrderApi/
tests/
  Sentinel.UnitTests/
docker-compose.yml
```

Create only the code required for today's vertical slice. Avoid adding empty services or abstractions solely to match the future architecture.

## Demo service

Create the following endpoints:

```http
GET  /health
GET  /orders/{id}
GET  /scenarios
GET  /scenarios/status
POST /scenarios/{scenarioId}/start
POST /scenarios/stop
```

### Required behavior

- `/health` reports whether the service is running.
- `/orders/{id}` returns a deterministic sample order while the service is healthy.
- `/scenarios/{scenarioId}/start` enables a controlled incident scenario.
- `/scenarios/stop` returns the service to normal behavior.
- When the fault is enabled, `/orders/{id}` returns a controlled error or delay.

Do not implement a real database connection leak in this milestone. A deterministic simulated failure is safer, faster to test, and sufficient to prove the initial telemetry flow.

### Required telemetry

Every request should emit or expose:

- Structured logs
- Trace ID
- Service version
- Request duration
- Current fault status

## Sentinel API

Create these endpoints:

```http
POST /incidents
GET  /incidents/{id}
```

Example incident request:

```json
{
  "title": "Order API latency increase",
  "service": "incidentlab-order-api",
  "startedAt": "2026-07-31T16:00:00Z",
  "severity": "High"
}
```

Store incidents in memory for this milestone. PostgreSQL persistence belongs in a later milestone.

## Domain model

Start with this minimal incident representation:

```csharp
public sealed record Incident(
    Guid Id,
    string Title,
    string Service,
    DateTimeOffset StartedAt,
    IncidentSeverity Severity,
    IncidentStatus Status);
```

Add enums for incident severity and status. Validate incoming requests in the application layer, including required text, supported enum values, and sensible timestamps.

## Observability

Configure both web projects with:

- OpenTelemetry ASP.NET Core instrumentation
- OpenTelemetry HTTP client instrumentation
- Console trace export for local verification
- Structured console logging

The first milestone does not require a dedicated telemetry backend. Console output is sufficient to prove that trace context and structured attributes are emitted correctly.

## Tests

Implement at least these automated tests:

- Incident creation validation
- Incident retrieval
- Demo fault enable and disable behavior
- Demo order endpoint behavior in healthy and faulty states

## Definition of done

The milestone is complete when all of the following are true:

- The solution builds from the command line.
- Automated tests pass.
- Both applications start independently.
- The demo endpoint succeeds when the fault is disabled.
- The demo endpoint fails or delays predictably when the fault is enabled.
- An incident can be created and retrieved.
- Logs contain structured fields rather than interpolated message-only text.
- Requests produce trace IDs and OpenTelemetry spans.
- Setup and verification commands are documented and reproducible.

## Not included today

- PostgreSQL or pgvector
- LLM integration
- RAG
- Multiple agents
- Aspire
- Web UI
- Kubernetes
- Kafka or RabbitMQ
- Redis
- Real production-system connectors
- Autonomous remediation

## Next milestone

After this slice is stable, add PostgreSQL persistence and define the `Evidence` contract. Then implement the first read-only connector that converts demo-service telemetry into provenance-preserving evidence.
