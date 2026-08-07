# Database

Sentinel uses PostgreSQL as the durable system of record and pgvector for
semantic retrieval indexes. The local environment pins PostgreSQL 18 and
pgvector 0.8.5 through the `pgvector/pgvector:0.8.5-pg18-bookworm` image.

## Local development

Start PostgreSQL:

```powershell
docker compose up -d postgres
```

Connection settings:

```text
Host: localhost
Port: 5432
Database: sentinel
Username: sentinel
Password: sentinel-local-dev-only
```

The password is intentionally a local-development default. Override it without
editing source-controlled files:

```powershell
$env:SENTINEL_POSTGRES_PASSWORD = "choose-a-local-password"
docker compose up -d postgres
```

The initialization script enables the `vector` extension when Docker creates a
new database volume. Verify the server and extension:

```powershell
docker compose exec postgres `
  psql -U sentinel -d sentinel `
  -c "SELECT current_setting('server_version'), extversion FROM pg_extension WHERE extname = 'vector';"
```

PostgreSQL data is stored in the `postgres-data` named volume. It survives:

```powershell
docker compose down
```

The following command deletes the local database and Tempo data and should be
used only when a full reset is intended:

```powershell
docker compose down -v
```

The initialization directory is only processed when PostgreSQL initializes an
empty data directory. Application schema changes use Entity Framework Core
migrations rather than additional initialization scripts.

## Application connection

The Sentinel container will use the internal Compose hostname:

```text
Host=postgres;Port=5432;Database=sentinel;Username=sentinel;Password=...
```

Docker Compose supplies this connection string to the API. The API applies
pending Entity Framework Core migrations during startup and uses the
PostgreSQL-backed incident repository for incident creation and retrieval.

The application currently persists:

- `incidents` as the durable incident context;
- `evidence` with its incident foreign key, type, source system, source
  reference, explicit source trace ID, span ID, service, observation time,
  summary, verification state, and SHA-256 content hash.

The unique `(incident_id, content_hash)` index prevents duplicate Evidence for
the same incident. A second unique index on incident, source trace ID, and
source span ID protects Tempo-import idempotency when normalization or hashing
rules evolve. Evidence persistence does not invoke an embedding model or
OpenAI. Semantic vector indexing remains a separate, optional asynchronous
stage.

Create a migration after changing a persistence mapping:

```powershell
dotnet ef migrations add MigrationName `
  --project src/Sentinel.Infrastructure/Sentinel.Infrastructure.csproj `
  --startup-project src/Sentinel.Api/Sentinel.Api.csproj `
  --output-dir Persistence/Migrations
```

Startup migration is suitable for the single-instance MVP. A production
multi-replica deployment should apply migrations as a separate controlled
deployment step so that replicas do not race during startup.
