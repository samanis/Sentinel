# Sentinel MVP Technical Specification

## 1. Purpose

This document is the implementation reference for the Sentinel minimum viable
product (MVP). It records what the team intends to build, the order in which it
will be built, the architectural boundaries, and the acceptance criteria used
to decide whether the MVP is complete.

Sentinel is an evidence-driven incident investigation system. It collects
observations from operational and engineering systems, converts them into
durable and source-backed evidence, uses agents to reason over that evidence,
and produces a human-reviewable root-cause analysis (RCA).

The MVP must prove one complete workflow with reproducible Incident Lab
scenarios. It is not intended to implement every component in the future-state
architecture.

## Implementation status

Last reviewed: 2026-08-04

Status notation:

- `[x]` Implemented and verified
- `[~]` Partially implemented or running with an interim adapter
- `[ ]` Not implemented

### Component status

- `[x]` .NET 10 solution and modular project boundaries
- `[x]` Sentinel incident domain model and application use cases
- `[x]` Sentinel incident API with Swagger, Problem Details, and health checks
- `[x]` Independent Incident Lab Order API
- `[x]` Incident Lab scenario engine
- `[x]` Slow database scenario
- `[x]` Database unavailable scenario
- `[x]` Dependency timeout scenario
- `[x]` Unhandled exception scenario
- `[x]` OpenTelemetry instrumentation in both applications
- `[x]` OpenTelemetry Collector with OTLP receivers, batching, and memory limits
- `[x]` Collector trace export to Tempo
- `[x]` Tempo 2.10.5 with persistent local storage and 24-hour retention
- `[x]` Separate Docker images for Sentinel API and Incident Lab
- `[x]` Docker Compose local deployment
- `[x]` Automated domain, application, and scenario-engine tests
- `[x]` PostgreSQL-backed incident persistence through the repository contract
- `[x]` PostgreSQL 18.4 and pgvector 0.8.5 deployment with persistent storage
- `[x]` Entity Framework Core persistence and initial incident migration
- `[x]` Evidence domain model with provenance and verification state
- `[x]` PostgreSQL evidence repository
- `[x]` Read-only Tempo connector inside Sentinel
- `[x]` Trace observation validation and deterministic Evidence normalization
- `[x]` Versioned Hypothesis and direct Evidence Relationship domain models
- `[x]` Bounded investigation analyzer contract and untrusted proposal models
- `[x]` All-or-nothing investigation analysis validation and domain mapping
- `[ ]` LLM relationship and hypothesis generation with deterministic validation
- `[x]` Manual Evidence creation and retrieval API
- `[x]` Loki log storage and read-only connector
- `[x]` Prometheus metric storage and read-only connector
- `[ ]` Deployment/change evidence connector
- `[ ]` Investigation orchestrator
- `[ ]` Persisted investigation timeline and hypotheses
- `[ ]` Trace analysis agent or tool
- `[ ]` Verification module or agent
- `[ ]` Root Cause Agent
- `[ ]` MCP Gateway
- `[ ]` First read-only MCP integration
- `[ ]` RAG and knowledge evidence
- `[ ]` RCA report generation
- `[ ]` Human review workflow
- `[ ]` Web UI beyond Swagger

### Verified end-to-end steps

- `[x]` Start the complete local stack with Docker Compose
- `[x]` Start and stop Incident Lab scenarios
- `[x]` Produce expected HTTP 200, 500, 503, and 504 outcomes
- `[x]` Emit structured scenario logs, traces, metrics, and exception details
- `[x]` Route application telemetry through the Collector
- `[x]` Persist Incident Lab traces in Tempo
- `[x]` Search Tempo using TraceQL
- `[x]` Retrieve a complete trace by trace ID
- `[x]` Route application logs through the Collector into Loki
- `[x]` Import warning/error Loki entries as atomic, idempotent Log Evidence
- `[x]` Route application metrics through the Collector into Prometheus
- `[x]` Import cumulative request, failure, and p95 latency Metric Evidence
- `[x]` Confirm a stored trace contains `DependencyTimeout` and HTTP 504
- `[x]` Restart Tempo and retrieve the same trace from persistent storage
- `[x]` Run the current automated suite successfully: 36 of 36 tests
- `[x]` Create and retrieve Sentinel incidents through PostgreSQL
- `[x]` Persist incidents through a Sentinel API restart
- `[x]` Persist Evidence through a Sentinel API restart
- `[x]` Reject duplicate Evidence using a deterministic content hash
- `[x]` Collect Tempo traces through the Sentinel API
- `[x]` Normalize Tempo error spans into provenance-preserving Evidence
- `[x]` Persist explicit Tempo trace ID, span ID, and service provenance
- `[x]` Commit each normalized trace import as one atomic Evidence batch
- `[x]` Emit structured trace import, not-found, validation, and source-failure logs
- `[ ]` Persist versioned hypotheses and Evidence relationships in PostgreSQL
- `[x]` Repeat Tempo collection without creating duplicate Evidence
- `[ ]` Correlate trace, log, metric, and deployment evidence
- `[ ]` Run an investigation over persisted evidence
- `[ ]` Generate and verify evidence-backed hypotheses
- `[ ]` Produce a cited RCA report
- `[ ]` Complete human review and finalization

The checklist is the authoritative progress summary. An item is marked complete
only after its implementation and proportionate verification are both present
in the repository. Update the review date whenever statuses change.

## 2. MVP outcome

The completed MVP must support this workflow:

```text
Start an Incident Lab scenario
        ↓
Generate affected order traffic
        ↓
Emit OpenTelemetry logs, metrics, and traces
        ↓
Store telemetry in searchable observability backends
        ↓
Create or detect a Sentinel incident
        ↓
Collect and normalize source-backed evidence
        ↓
Persist normalized evidence
        ↓
Build a timeline and candidate hypotheses
        ↓
Verify hypotheses against evidence
        ↓
Generate a cited RCA and recommendations
        ↓
Require human review before finalization
```

The MVP succeeds when this workflow is repeatable, auditable, and produces
materially consistent conclusions for the fixed evaluation scenarios.

## 3. Architecture strategy

Sentinel will be implemented as a modular monolith for the MVP. Logical modules
have explicit contracts and testable boundaries, but they run in one Sentinel
application unless separate deployment is already required by the nature of the
component.

Separately deployed components are:

- Sentinel API
- Incident Lab Order API
- OpenTelemetry Collector
- Tempo
- Loki
- Prometheus-compatible metrics backend
- PostgreSQL with pgvector
- Optional local Grafana UI

The investigation orchestrator, evidence normalizer, agent runtime, MCP gateway,
verification logic, RCA logic, and report generation remain modules inside the
Sentinel application during the MVP.

Kafka, RabbitMQ, Redis, independently deployed agents, and a separate API
gateway are not MVP requirements. They may be introduced later only when
measured scaling, isolation, security, reliability, or ownership requirements
justify their operational cost.

The visual reference is [MVP-Architecture-v2.png](../MVP-Architecture-v2.png).

## 4. System components

### 4.1 Incident Lab Order API

Incident Lab is an independently runnable external sample system. It must not
reference Sentinel projects or share Sentinel process memory. Its purpose is to
generate controlled, repeatable incident signals.

Implemented scenarios:

| Scenario | Expected behavior |
| --- | --- |
| `slow-database` | HTTP 200 after a configurable delay; warning log and slow span |
| `database-unavailable` | HTTP 503 after a configurable delay; failed span and error log |
| `dependency-timeout` | HTTP 504 after a configurable delay; failed span and error log |
| `unhandled-exception` | HTTP 500; exception event, stack trace, and error log |

Scenario API:

```http
GET  /scenarios
GET  /scenarios/status
POST /scenarios/{scenarioId}/start
POST /scenarios/stop
GET  /orders/{id}
GET  /health
```

Scenario requirements:

- Only one scenario is active at a time in the first version.
- A scenario has a configurable duration and delay.
- A scenario expires automatically and can also be stopped explicitly.
- Repeated execution with the same inputs produces materially equivalent
  symptoms.
- Every affected request is tagged with the scenario name.
- The service emits structured logs, traces, custom metrics, service identity,
  version, and environment metadata.
- Incident Lab returns to a healthy state after scenario cleanup.

Later Incident Lab scenarios may include intermittent failure, traffic spike,
CPU saturation, memory pressure, bad deployment, cascading failure, and process
crash. They are not required for the first complete investigation workflow.

### 4.2 OpenTelemetry Collector

Applications emit OTLP telemetry to the official OpenTelemetry Collector image.
The Collector is configured through repository-owned YAML; Sentinel does not
implement a custom Collector in C#.

Collector responsibilities:

- Receive OTLP over gRPC and HTTP.
- Apply memory limits and batching.
- Route traces to Tempo.
- Route logs to Loki through its native OTLP HTTP endpoint.
- Route metrics to the Prometheus-compatible backend when introduced.
- Retain the debug exporter for local troubleshooting where useful.
- Own exporter retries and backend-specific transport configuration.

The Collector is a router and processor, not a searchable data store.

### 4.3 Tempo

Tempo is the raw trace system of record for its configured retention period.
The local MVP uses monolithic Tempo with persistent local storage in a named
Docker volume.

Tempo responsibilities:

- Receive traces only from the Collector.
- Store complete traces and spans.
- Support TraceQL search by service, time window, status, attributes, and trace
  ID.
- Return a full trace by trace ID.

Sentinel queries Tempo using a read-only connector. Sentinel never treats Tempo
as its evidence repository.

### 4.4 Loki and metrics backend

Loki 3.7.4 stores logs for the local MVP using TSDB and persistent filesystem
storage. The Collector sends logs to Loki's native OTLP HTTP endpoint. Sentinel
queries Loki read-only by incident service and a caller-supplied, maximum
24-hour time window. It validates Loki's stream response, maps labels and
structured metadata into canonical log observations, selects warning, error,
fatal, and critical entries, and atomically persists them as Log Evidence.
Raw log streams remain in Loki.

Prometheus 3.13.1 stores metrics for seven days and accepts Collector remote
write. Sentinel queries three bounded, scenario-labelled snapshots at the end
of the requested window: cumulative requests, cumulative failures, and
cumulative-histogram p95 latency. They are deliberately described as
cumulative values because Sentinel does not yet establish a pre-incident
counter baseline. Claiming these values are incident-window deltas would be
incorrect.

The MVP must eventually correlate:

- Trace and span IDs
- Service identity
- Incident time window
- HTTP status and duration
- Exception events
- Structured log properties
- Metric names and label sets
- Deployment or version metadata

### 4.5 Sentinel API

The API is the external boundary for incident and investigation operations.

MVP API capabilities:

- Create and retrieve incidents.
- Start evidence collection for an incident.
- Retrieve accepted evidence for an incident.
- Start or retrieve an investigation.
- Retrieve hypotheses, timeline, RCA, and report status.
- Submit human review decisions.
- Expose health checks, OpenAPI, Swagger, and Problem Details responses.

The API does not proxy raw Tempo, Loki, Prometheus, or MCP responses. It invokes
application use cases, which call connectors, normalize results, persist
Sentinel-owned records, and return Sentinel contracts.

### 4.6 PostgreSQL and pgvector

PostgreSQL is the durable system of record for Sentinel-owned state.

MVP data includes:

- Incidents
- Evidence
- Evidence provenance and lifecycle
- Investigations and execution state
- Timeline entries
- Hypotheses and their supporting or contradicting evidence
- Agent and model execution audit records
- Human review decisions
- RCA reports
- Knowledge-document metadata and embeddings when RAG is introduced

`jsonb` is used for bounded source-specific attributes and provenance. pgvector
is used for semantic retrieval indexes. A vector is never the authoritative
copy of an Evidence record or hypothesis.

The MVP will use Entity Framework Core migrations. Development and test startup
must not depend on manually created tables.

## 5. Evidence architecture

### 5.1 Trust hierarchy

Sentinel separates facts from interpretations and conclusions:

```text
Source artifact
      ↓
Validated observation
      ↓
Normalized, source-backed Evidence
      ↓
AI interpretation
      ↓
Candidate hypothesis
      ↓
Verified and human-reviewed conclusion
```

AI is used for semantic reasoning, pattern recognition, hypothesis generation,
comparison, and reporting. AI is not the authoritative source for status codes,
timestamps, durations, trace IDs, service names, or other directly extractable
facts.

### 5.2 Evidence contract

An accepted Evidence record contains at least:

```text
Id
IncidentId
Type
Summary
SourceSystem
SourceReference
ObservedAt
CollectedAt
Reliability
Attributes
Provenance
NormalizerVersion
ContentHash
Status
```

Evidence types initially include `Trace`, `Log`, `Metric`, `Deployment`, and
`Knowledge`. The implementation begins with `Trace`.

Evidence status lifecycle:

```text
Collected → Accepted → Superseded
          ↘ Rejected
```

- Accepted evidence is consumed by agents by default.
- Accepted Evidence is not silently rewritten.
- A normalization change creates a new evidence version and may supersede the
  previous record.
- Invalid evidence is rejected but retained for audit history.

### 5.3 Source reference and provenance

Every evidence item must provide a stable pointer to the source artifact. Trace
evidence uses the Tempo trace and span IDs, for example:

```text
trace:a0585f4b43067735932ec9d2366bff82/span:abc123
```

Provenance includes:

- Connector name and version
- Query and query filters
- Collection time
- Source system
- Trace/span or equivalent source identifiers
- Transformations applied
- Normalizer name and version
- Content hash

Telemetry and MCP responses are untrusted external input. Connectors validate
size, required fields, timestamps, identifiers, attribute count, and supported
types before creating observations.

### 5.4 Normalization pipeline

Normalization is deterministic:

```text
Tempo JSON
    ↓ deserialize and validate
Tempo-specific models
    ↓ extract canonical fields
TraceObservation
    ↓ deterministic evidence rules
Evidence record with a factual summary
    ↓ deduplicate and persist
PostgreSQL
```

The Tempo response model remains in Infrastructure. Application contracts do
not expose Tempo JSON details.

A technology-neutral trace observation contains:

```text
TraceId
SpanId
ServiceName
OperationName
StartedAt
Duration
Status
HttpStatusCode
Scenario
Exception
CanonicalAttributes
```

Attribute aliases are mapped into stable Sentinel keys. For example,
`http.response.status_code` is exposed as `http.status_code` in Evidence.

### 5.5 Factual Evidence summaries

Evidence summaries use deterministic templates and state only what the source
directly demonstrates.

Examples:

```text
GET /orders/{id} in incidentlab-order-api returned HTTP 504 after 257 ms.

The orders database dependency span ended with an error after 250 ms.

Order processing recorded System.InvalidOperationException.
```

A trace showing HTTP 504 does not, by itself, support a hypothesis that a
database server crashed. Causal language belongs in hypotheses and conclusions.

### 5.6 Persistence and idempotency

Evidence is persisted immediately in PostgreSQL. It is not regenerated each
time an agent runs.

Collection is idempotent. A unique source identity is based on:

```text
IncidentId + SourceSystem + SourceReference + EvidenceType + NormalizerVersion
```

A content hash provides an additional integrity and change-detection mechanism.
Repeating an unchanged collection query must not create duplicate evidence.

Raw telemetry remains in its observability backend for the configured retention
period. Sentinel persists normalized evidence because investigations and reports
must remain reproducible after raw telemetry expires.

## 6. Connector architecture

External systems are represented by read-only source interfaces, not Sentinel
repositories.

```text
ITraceSource             → TempoTraceClient
ILogSource               → LokiLogClient
IMetricSource            → PrometheusMetricClient
IDeploymentSource        → native or MCP-backed implementation
IKnowledgeSource         → RAG or MCP-backed implementation
```

Repositories represent storage owned by Sentinel:

```text
IIncidentRepository
IEvidenceRepository
IInvestigationRepository
IReportRepository
```

The collection dependency flow is:

```text
Evidence API endpoint
    ↓
CollectTraceEvidenceUseCase
    ├── IIncidentRepository
    ├── ITraceSource
    │       ↓
    │   TempoTraceClient
    │       ↓
    │   Tempo HTTP API
    ├── TraceEvidenceNormalizer
    └── IEvidenceRepository
            ↓
        PostgreSQL
```

Connector requirements:

- Read-only credentials and operations by default
- Explicit timeouts and cancellation
- Bounded query windows and response sizes
- Retry only for safe transient failures
- Structured audit logging
- Transparent partial-failure reporting
- No fabricated data when a source is unavailable

## 7. Agent and orchestration architecture

Agents consume accepted, persisted Evidence. They do not become the source of
Evidence, rewrite normalized facts, or receive unrestricted access to
production systems.

### 7.1 Investigation orchestrator

The orchestrator is an application module responsible for:

- Creating an investigation plan
- Selecting the required tools or agents
- Enforcing time, token, and cost budgets
- Passing incident scope and evidence references
- Handling cancellation, timeout, retries, and partial results
- Recording every agent and model execution
- Maintaining investigation state
- Supporting idempotent re-execution

The initial implementation may use a fixed deterministic plan. Dynamic planning
is introduced only after the fixed workflow is measurable and reliable.

### 7.2 MVP agent sequence

The first end-to-end sequence is:

```text
Trace evidence collection
        ↓
Trace analysis
        ↓
Deterministic verification
        ↓
Root Cause Agent
        ↓
Report generation
        ↓
Human review
```

Log, metrics, deployment, and knowledge tools are added incrementally. Logical
specialization does not require separate processes or containers.

### 7.3 Agent output contract

Agent findings are not Evidence. A finding contains:

- Finding ID
- Investigation ID
- Agent role and version
- Statement
- Supporting evidence IDs
- Contradicting evidence IDs
- Confidence rationale
- Limitations and missing data
- Model identifier
- Prompt version
- Generated time
- Tool activity references

Confidence is not presented as a calibrated probability until an evaluation
dataset demonstrates calibration.

### 7.4 Verification

Verification must:

- Confirm every factual statement resolves to evidence.
- Reject citations that do not support the statement.
- Identify contradictions and missing evidence.
- Deduplicate materially equivalent findings.
- Compare candidate hypotheses.
- Prevent an unsupported interpretation from becoming an accepted conclusion.

Deterministic checks are preferred where possible. An AI verifier may supplement
but not replace source-reference validation.

## 8. MCP architecture

MCP is the standardized tool-access layer for suitable external engineering and
knowledge systems. It is not the transport for all raw, high-volume telemetry.

Native connectors are preferred for Tempo, Loki, and Prometheus because their
query APIs are predictable and data-intensive. MCP is initially appropriate for
systems such as:

- GitHub
- Kubernetes
- Jira
- Slack or Teams
- Runbook and documentation stores

MCP flow:

```text
External system
    ↓
Read-only MCP server/tool
    ↓
Sentinel MCP Gateway
    ↓ validate and authorize
Typed observation
    ↓ normalize
Evidence
```

MCP Gateway responsibilities:

- Server and tool registry
- Authentication and credential isolation
- Independent authorization enforcement
- Read-only policy by default
- Input and output validation
- Timeout, rate limit, and response-size limits
- Prompt-injection defenses
- Redaction of sensitive content
- Audit logging
- Conversion to typed observations

The first MCP milestone should add one read-only integration after the trace
evidence workflow works. GitHub deployment/change lookup or Kubernetes resource
inspection are suitable candidates.

## 9. Knowledge retrieval and RAG

RAG adds targeted operational context from runbooks, architecture decisions,
service documentation, prior incidents, and API documentation.

RAG requirements:

- Index only authorized documents.
- Preserve document ID, version, location, and access scope.
- Store document metadata and embeddings in PostgreSQL/pgvector initially.
- Retrieve small, relevant passages rather than full documents.
- Treat retrieved text as untrusted input and defend against prompt injection.
- Create Knowledge evidence with resolvable citations.
- Keep the source text authoritative; embeddings are retrieval indexes only.

RAG is added after operational evidence collection works. It must not become a
substitute for trace, log, metric, or deployment evidence.

## 10. Human review and safety

The MVP does not perform autonomous remediation.

Human review is required before:

- Marking an RCA as final
- Publishing a postmortem
- Sending notifications outside the local demo
- Creating or updating external tickets
- Recommending a consequential action as approved
- Executing any external write operation

The reviewer must see:

- Incident summary
- Timeline
- Candidate hypotheses
- Selected root cause and confidence rationale
- Supporting and contradicting evidence
- Missing data and degraded integrations
- Recommendations and risk notes
- Model, prompt, connector, and tool audit information

## 11. API shape

The exact resource design may evolve, but the MVP needs capabilities equivalent
to:

```http
POST /incidents
GET  /incidents/{incidentId}

POST /incidents/{incidentId}/evidence/collect
GET  /incidents/{incidentId}/evidence

POST /incidents/{incidentId}/investigations
GET  /investigations/{investigationId}
GET  /investigations/{investigationId}/timeline
GET  /investigations/{investigationId}/hypotheses
GET  /investigations/{investigationId}/report

POST /investigations/{investigationId}/review
```

Long-running investigation execution may later become asynchronous. The first
vertical slice may run synchronously if execution remains bounded, while still
persisting explicit investigation state.

## 12. Storage model

Initial PostgreSQL tables:

### 12.1 `incidents`

Stores incident identity, title, affected service, severity, status, start time,
creation time, and lifecycle metadata.

### 12.2 `evidence`

Stores the Evidence contract, factual summary, source reference, normalized
attributes, provenance, normalizer version, content hash, and lifecycle status.

Required constraints include:

- Foreign key to incident
- Unique source identity per normalizer version
- Bounded string lengths
- Non-null observed and collection times
- Valid lifecycle status

### 12.3 Later MVP tables

Add these when their behavior is implemented:

- `investigations`
- `timeline_entries`
- `hypotheses`
- `hypothesis_evidence`
- `agent_executions`
- `model_invocations`
- `reports`
- `review_decisions`
- `knowledge_documents`
- `knowledge_chunks`

Do not create empty speculative tables merely to match the future-state diagram.

## 13. Observability of Sentinel

Sentinel and Incident Lab both emit their own OpenTelemetry signals. Resource
identity must distinguish them:

```text
sentinel-api
incidentlab-order-api
```

Sentinel telemetry must include:

- API request spans and metrics
- External connector calls
- Evidence collection counts and failures
- Normalization results and rejection counts
- Investigation and agent execution spans
- Model latency, token usage, and cost where available
- Retry, timeout, cancellation, and degraded-result events
- Correlation identifiers for incident and investigation IDs

Secrets, full prompts containing sensitive source content, and unrestricted raw
tool responses must not be written to logs.

## 14. Security requirements

- External connectors and MCP tools are read-only by default.
- Credentials remain in deployment configuration or a secret manager, not source
  control or agent context.
- Authorization is enforced by application and integration boundaries, not by
  model instructions alone.
- Query scope, time window, response size, and attribute count are bounded.
- Telemetry, documents, and tool responses are treated as untrusted input.
- Sensitive values are redacted before persistence or model use.
- Evidence access is auditable.
- External writes require explicit human approval and are outside the initial
  MVP.
- Tempo's unauthenticated local endpoint must not be exposed as-is in a public
  production environment.

## 15. Reliability requirements

- All connector and model operations accept cancellation tokens.
- Timeouts are explicit and configurable.
- Retries use bounded exponential backoff and only apply to safe operations.
- Circuit breakers are introduced when real external dependencies exist.
- Evidence collection is idempotent.
- Investigations can resume or restart without duplicating durable state.
- Missing sources produce transparent partial results.
- An unavailable AI provider does not destroy collected evidence.
- Health checks distinguish application liveness from dependency readiness.

## 16. Evaluation strategy

Each Incident Lab scenario has a private evaluation manifest that is not passed
to the investigation agents. It records:

- Scenario ID and configuration
- Known root cause
- Expected HTTP behavior
- Expected trace, log, and metric signals
- Expected deployment context
- Required and optional evidence
- Cleanup behavior
- Acceptable RCA terminology

Evaluation measures:

- Whether the known root cause appears in ranked hypotheses
- Whether cited evidence supports the conclusion
- Whether citations resolve to source artifacts
- Whether the timeline preserves event ordering
- Whether repeated runs are materially consistent
- Investigation latency
- Model token usage and cost
- Behavior under missing or unavailable integrations
- Rate of unsupported factual statements

The evaluation oracle is used only to score the result. It is not exposed to the
agent as evidence.

## 17. Delivery sequence

### Milestone 1 — Foundation and Incident Lab

Status: substantially implemented.

- .NET solution and modular project structure
- Incident domain and application use cases
- Sentinel API, Swagger, health checks, and Problem Details
- Independent Incident Lab Order API
- Scenario engine and four deterministic scenarios
- OpenTelemetry instrumentation
- Collector deployment
- Separate application Docker images
- Docker Compose local stack
- Automated unit tests

### Milestone 2 — Durable traces

Status: implemented.

- Tempo 2.10.5 in Docker Compose
- Persistent local Tempo volume
- Collector trace export to Tempo
- TraceQL search and trace-by-ID verification
- Dependency-timeout trace containing scenario and HTTP 504
- Persistence verified across a Tempo restart

### Milestone 3 — Durable incident and trace evidence

Next implementation milestone.

- [x] PostgreSQL with pgvector in Docker Compose
- [x] EF Core provider and migrations
- [x] Replace the production in-memory incident repository with PostgreSQL
- [x] Evidence domain model, provenance, and verification state
- [x] PostgreSQL evidence repository
- [x] `ITraceSource` application abstraction
- [x] Tempo read-only client
- [x] Tempo response validation and canonical trace observation
- [x] Deterministic trace Evidence normalizer and factual summary
- [x] Manual Evidence creation and retrieval endpoints
- [x] Deterministic Evidence content hashing and deduplication
- [x] Sentinel and Evidence restart persistence tests
- [x] Explicit trace/span/service provenance fields and indexes
- [x] Atomic all-or-nothing trace Evidence persistence
- [x] Structured Tempo import lifecycle and validation logging

Acceptance scenario:

```text
Run dependency-timeout
→ receive HTTP 504
→ create incident
→ collect Tempo evidence
→ persist trace Evidence in PostgreSQL
→ restart Sentinel
→ retrieve incident and evidence
→ collect again
→ confirm no duplicate evidence
```

### Milestone 4 — Logs, metrics, and deployment evidence

- [x] Loki deployment and Collector native OTLP log export
- [x] Persistent local Loki storage with seven-day retention
- [x] Read-only `ILogSource` and Loki query-range adapter
- [x] Canonical log observations with service, severity, body, trace, and span provenance
- [x] Deterministic warning/error Log Evidence normalization
- [x] Atomic, idempotent Loki Evidence import API
- [x] Cross-source Evidence coexistence for Tempo and Loki
- [x] Prometheus 3.13.1 deployment and Collector remote-write export
- [x] Read-only `IMetricSource` and Prometheus HTTP API adapter
- [x] Canonical cumulative metric observations and deterministic Metric Evidence
- [x] Atomic, idempotent Prometheus Evidence import API
- Deployment/change evidence source
- Cross-signal correlation by service, time, trace, version, and incident
- Evidence relevance and bounded collection rules

### Milestone 5 — Investigation and first agent workflow

- Persisted investigation state
- Fixed investigation plan
- Timeline construction
- Trace analysis tool or Trace Agent
- Deterministic evidence verification
- Root Cause Agent
- Hypothesis and supporting-evidence persistence
- Model and prompt version audit
- Time, token, and cost budgets
- Transparent partial results

### Milestone 6 — MCP and knowledge retrieval

- MCP Gateway module
- One read-only MCP integration
- Tool registry, authorization, validation, and audit
- Runbook/document ingestion
- pgvector embeddings and scoped retrieval
- Knowledge evidence with source citations
- Prompt-injection defenses

### Milestone 7 — Report and human review

- Executive RCA summary
- Evidence-linked incident timeline
- Ranked hypotheses and confidence rationale
- Recommendations with risk and effort notes
- Postmortem draft
- Human approval/rejection workflow
- Final report persistence
- Reproducible demo script and evaluation report

## 18. MVP definition of done

The MVP is complete when:

- A clean checkout starts with documented Docker Compose commands.
- Database migrations apply automatically or through one documented command.
- All automated tests pass.
- Incident Lab scenarios are reproducible and clean up safely.
- Logs, metrics, and traces reach searchable local backends.
- Sentinel persists incidents and accepted evidence in PostgreSQL.
- Repeated collection does not duplicate evidence.
- Evidence has deterministic factual summaries, source references, and provenance.
- A complete investigation can be executed for at least the fixed dependency
  timeout scenario.
- The RCA identifies the known cause in its ranked hypotheses.
- Every factual report statement is supported by resolvable evidence.
- Missing integrations produce visible degraded results, not fabricated data.
- Model, prompt, tool, and connector activity is auditable.
- Human review is required before the RCA is finalized.
- The system remains usable after Sentinel and storage containers restart.
- Setup, architecture, scenario execution, and evaluation are documented.

## 19. Explicit MVP non-goals

- Autonomous remediation
- Unrestricted write access to external systems
- Every future specialized agent
- Separate deployment for every logical module
- Kafka or RabbitMQ without a demonstrated requirement
- Redis without a distributed coordination requirement
- Multi-region or high-availability production deployment
- Support for every telemetry vendor
- General-purpose chatbot behavior
- Self-modifying or self-learning agents
- LLM-generated Evidence or mutation of source-backed Evidence
- Calibrated probability claims without evaluation data
- AWS, Azure, or GCP dependency for local execution
- Full compliance, security, cost, capacity, or chaos-analysis suites

## 20. Architectural decisions to preserve

1. Evidence is authoritative; agent output is interpretation.
2. Normalized Evidence facts are deterministic, persisted, source-backed, and
   not silently rewritten.
3. Raw telemetry remains in observability backends; normalized evidence remains
   in PostgreSQL.
4. Native connectors handle high-volume observability queries; MCP handles
   suitable standardized engineering and knowledge tools.
5. Connector and MCP responses are untrusted until validated and normalized.
6. Agents consume accepted evidence and cite evidence IDs.
7. The MVP is a modular monolith, not a collection of premature microservices.
8. Incident Lab is an external process/container and has no Sentinel project
   references.
9. AI performs semantic reasoning where it adds value; deterministic code
   establishes directly extractable facts.
10. Human approval remains the boundary for final conclusions and external
    actions.

## 21. Immediate next action

Build the application-layer investigation analysis contract and deterministic
validator over Tempo Trace Evidence, Loki Log Evidence, and Prometheus Metric
Evidence. LLM-generated relationships and hypotheses must cite existing
Evidence IDs, preserve metric scope, and record model and prompt versions.
