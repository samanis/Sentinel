# Sentinel on Kubernetes

This first stage deploys the existing API and PostgreSQL to the **existing**
Minikube context. It creates no cluster, AWS resource, ingress, CI deployment,
or GitHub Actions runner. The shared module accepts `environment = "local"`
or `"aws"`; separate roots make cloud creation impossible from the local root.

## Repository inspection

No applicable `AGENTS.md` was found in the repository or its ancestor directories.
The solution is `Sentinel.sln` (.NET 10; `global.json` selects SDK 10.0.101).

| Component | Project / Dockerfile | Ports and health | Dependencies |
| --- | --- | --- | --- |
| API | `src/Sentinel.Api/Sentinel.Api.csproj`, `src/Sentinel.Api/Dockerfile` | Container 8080; Compose host 5156; existing `GET /`, `/health`; new `/health/ready` | PostgreSQL on startup, migrations; Tempo/Loki/Prometheus for evidence imports; Ollama or OpenAI for RCA |
| Worker | `src/Sentinel.Worker/Sentinel.Worker.csproj`, `src/Sentinel.Worker/Dockerfile` | Background process, no HTTP listener or health endpoint | PostgreSQL, Loki, Tempo, Ollama embeddings; migrates at startup |
| RAG API | `src/Sentinel.RagApi/Sentinel.RagApi.csproj`, `src/Sentinel.RagApi/Dockerfile` | Container 8080; Compose host 5157; `GET /`, `/health` | Existing schema, PostgreSQL read-only connection, embeddings and answer model |
| PostgreSQL | Compose image `pgvector/pgvector:0.8.5-pg18-bookworm` | Container 5432; Compose host 5433; `pg_isready` | Volume mounted at `/var/lib/postgresql` (PostgreSQL 18 layout) |
| Ollama | Host service, **no repository Dockerfile/container version** | 11434 | `embeddinggemma`, 768 dimensions; default answer model `qwen3:8b`, context 8192, max output 1000 |
| Samples | `samples/IncidentLab.OrderApi`, `samples/IncidentLab.TelemetryGenerator`; each has its own csproj and Dockerfile | Container 8080; Compose host 5112 / 5113 | OTLP collector, generator targets Order API |

Configuration comes from each project's `appsettings*.json` and environment
overrides in `docker-compose.yml`. API production configuration requires
`ConnectionStrings__Sentinel`; development defaults are deliberately not used.
Relevant settings are `Tempo__BaseUrl`, `Loki__BaseUrl`,
`Prometheus__BaseUrl`, `AI__Provider`, `Ollama__BaseUrl`,
`Ollama__Model`, `Embedding__BaseUrl`, `Embedding__Model`,
`Embedding__Dimensions`, and `OTEL_EXPORTER_OTLP_ENDPOINT`.

Compose also defines OTEL Collector (4317/4318, health 13133), Tempo (3200),
Loki (3100), Prometheus (9090), Alertmanager (9093), Grafana (3000), and their
configuration under `deploy/`. They remain in Compose for now.

## Deliberate first-stage boundaries

API incident creation/read, manual evidence operations and durable alert intake
work with PostgreSQL alone. Collected telemetry, background processing, semantic
search and AI analysis are **not** supplied by this deployment. Webhook work
remains pending without the worker. Default localhost telemetry/model URLs
do not refer to host services inside a Pod. No model/API key is needed for the
usable persistence endpoints.

Before adding Ollama, supply an approved container version/digest, CPU/RAM
budget, CPU-versus-GPU decision (and GPU runtime if required), model disk
capacity/storage class, download policy and pinned model revisions. The repo
specifies model names but not those deployment requirements. A later Ollama
Deployment must mount persistent model storage at `/root/.ollama` and provision
both models before enabling the worker/RAG. Do not replace neural embeddings
with placeholder vectors. Worker/RAG additionally need reachable Loki/Tempo
and an OTLP destination (plus Prometheus for API metric imports).

## Windows 11: build, review, deploy

Run PowerShell from the repository root, with Docker Desktop and your existing
Minikube running. Required tools: Terraform >=1.6, kubectl, Docker; .NET 10
for tests. The Minikube CLI is preferred for loading images but is optional for
the verified single-node Docker/containerd fallback.

```powershell
kubectl config get-contexts
kubectl --context=minikube get nodes -o wide
kubectl --context=minikube get storageclass
kubectl --context=minikube get apiservice v1beta1.metrics.k8s.io

# Preferred when minikube is on PATH:
./deploy/terraform/scripts/Build-LocalImage.ps1
# OR, for the existing single Docker node named minikube using containerd:
./deploy/terraform/scripts/Build-LocalImage.ps1 -DockerNodeName minikube

terraform -chdir=deploy/terraform/local init
terraform -chdir=deploy/terraform/local validate

# One-time bootstrap: Terraform owns the namespace; credentials stay outside state.
terraform -chdir=deploy/terraform/local plan '-target=module.sentinel.kubernetes_namespace_v1.sentinel' '-out=namespace.tfplan'
terraform -chdir=deploy/terraform/local show namespace.tfplan
terraform -chdir=deploy/terraform/local apply namespace.tfplan
./deploy/terraform/scripts/Initialize-LocalSecret.ps1

# Full workload plan. Inspect before applying.
terraform -chdir=deploy/terraform/local plan '-out=local.tfplan'
terraform -chdir=deploy/terraform/local show local.tfplan
terraform -chdir=deploy/terraform/local apply local.tfplan
```

The build script generates a unique revision/time/random image tag, loads it
into the existing node, and writes ignored `local/image.auto.tfvars.json`.
Subsequent builds followed by plan/apply update the Deployment. No `latest`
tags are used. Dockerfile base images retain the repository's existing .NET
10 tags; fully reproducible base-image builds would additionally require
digest pinning. Do not run both image-build alternatives.

The initial targeted apply is intentional only to create the namespace before
the out-of-band Secret; all subsequent plans/applies are full plans.
The Secret initializer uses a generated random password over stdin, preserving
an existing Secret. It never sends credentials as command-line arguments or
Terraform values, and never reads secret content. Kubernetes administrators
can still read Kubernetes Secrets; protect cluster access and backups.
Do not delete/regenerate the Secret while retaining the initialized database:
PostgreSQL does not reset its password from the environment on an existing PVC.

PostgreSQL uses a 10Gi PVC, a single replica and `Recreate` strategy. Its requests
are 100m CPU/256Mi RAM, limits 1 CPU/1Gi. API requests are 100m/256Mi, limits
1 CPU/512Mi. These are initial local budgets, not performance sizing.
The API's existing startup migrations create the pgvector extension and schema;
the Compose initialization SQL is not needed here. A five-minute startup probe
allows migrations; liveness uses `/health`, readiness uses database connectivity.
EF migrations remain in the API, preserving the current architecture.

## Verify

The repeatable verification script starts and stops its own hidden port-forward
and creates a smoke-test incident. The optional recovery check briefly stops
only this deployment's PostgreSQL and restores it in a finally block:

```powershell
./deploy/terraform/scripts/Test-LocalDeployment.ps1
# Run when a brief database interruption is acceptable:
./deploy/terraform/scripts/Test-LocalDeployment.ps1 -TestDatabaseRecovery
```

See [recorded validation results](VALIDATION.md). For manual inspection:

```powershell
kubectl --context=minikube -n sentinel-local rollout status deployment/postgres --timeout=180s
kubectl --context=minikube -n sentinel-local rollout status deployment/sentinel-api --timeout=180s
kubectl --context=minikube -n sentinel-local get deployments,pods,services,pvc
kubectl --context=minikube -n sentinel-local describe deployment sentinel-api
kubectl --context=minikube -n sentinel-local get endpointslices
kubectl --context=minikube -n sentinel-local logs deployment/sentinel-api --tail=30
kubectl --context=minikube -n sentinel-local exec deployment/postgres -- psql -U sentinel -d sentinel -c "SELECT extname, extversion FROM pg_extension WHERE extname = 'vector';"

# Leave this terminal open. 15156 avoids the Compose host port.
kubectl --context=minikube -n sentinel-local port-forward service/sentinel-api 15156:8080
```

In a second terminal:

```powershell
Invoke-RestMethod http://localhost:15156/
Invoke-RestMethod http://localhost:15156/health
Invoke-RestMethod http://localhost:15156/health/ready
$body = @{
    title = 'Minikube deployment verification'
    service = 'sentinel-deployment-smoke'
    startedAt = [DateTimeOffset]::UtcNow.ToString('o')
    severity = 'Low'
} | ConvertTo-Json
$incident = Invoke-RestMethod -Method Post -Uri http://localhost:15156/api/incidents/ -ContentType application/json -Body $body
Invoke-RestMethod "http://localhost:15156/api/incidents/$($incident.id)"
dotnet test Sentinel.sln --no-restore
terraform fmt -check -recursive deploy/terraform
terraform -chdir=deploy/terraform/local plan -detailed-exitcode
# Last command: 0 = no drift, 2 = review changes, 1 = error.
```

The smoke test creates an intentional incident record. Readiness failure can
be checked during a maintenance window by scaling PostgreSQL to zero, checking
that `/health` stays 200 while `/health/ready` becomes 503, then scaling back to
one and waiting for readiness. Never use liveness for database availability.

## HPA

The inspected cluster has no metrics-server. HPA is therefore off by default.
After metrics-server is available and reporting node/pod metrics:

```powershell
kubectl --context=minikube get apiservice v1beta1.metrics.k8s.io
kubectl --context=minikube top nodes
terraform -chdir=deploy/terraform/local plan -var=enable_hpa=true '-out=hpa.tfplan'
terraform -chdir=deploy/terraform/local show hpa.tfplan
terraform -chdir=deploy/terraform/local apply hpa.tfplan
kubectl --context=minikube -n sentinel-local get hpa
```

Persist `enable_hpa = true` in an ignored local `.tfvars` file if enabled.
The HPA targets 70% requested CPU with 1-3 replicas. Terraform ignores replica
drift so it does not fight the HPA. When disabling HPA, explicitly scale the API
back to one if desired. HPA load behavior is not tested without metrics-server.

## Cleanup and persistence

Stop port-forward with Ctrl+C. To suspend without losing data:

```powershell
kubectl --context=minikube -n sentinel-local scale deployment/sentinel-api --replicas=0
kubectl --context=minikube -n sentinel-local scale deployment/postgres --replicas=0
# Disable an enabled HPA first, otherwise it can scale the API back up.
# Resume:
kubectl --context=minikube -n sentinel-local scale deployment/postgres --replicas=1
kubectl --context=minikube -n sentinel-local scale deployment/sentinel-api --replicas=1
```

A full destroy intentionally fails while the PVC has `prevent_destroy = true`.
For **permanent deletion of this deployment and its database**, back up first,
change that single lifecycle setting to `false` in
`modules/sentinel/main.tf`, then:

```powershell
terraform -chdir=deploy/terraform/local plan -destroy '-out=destroy.tfplan'
terraform -chdir=deploy/terraform/local show destroy.tfplan
terraform -chdir=deploy/terraform/local apply destroy.tfplan
```

Restore `prevent_destroy = true` afterward. Namespace deletion also removes
the out-of-band Secret. Minikube's default storage class has Delete reclaim
policy: deleting its PVC deletes database storage. The guard does not protect
against direct kubectl namespace deletion or deleting Minikube itself.
No `minikube delete` is needed; the existing cluster remains intact.
Keep ignored state files until cleanup completes; never commit state or plans.

## AWS review only

`aws/infrastructure` has only the AWS provider: EKS, node IAM, private-endpoint
cluster, managed node group, deployment-role access and immutable-tag ECR.
It requires a region, cluster name, supported Kubernetes version, two or more
existing private subnets across AZs, deployment IAM role ARN and node instance
types. No VPC/NAT is implicitly created. Review network egress, private endpoint
access, IAM scope (initial deployer has cluster-admin), costs, remote encrypted
state/locking, logging retention, add-ons and security before any AWS plan/apply.
CNI uses node IAM in this draft; consider separate pod identity at review.

`aws/workloads` uses only the Kubernetes provider and the same Sentinel module
with `environment = "aws"`; it does **not** deploy local PostgreSQL. Supply an
EKS kubeconfig context, pushed ECR image, an externally managed
`sentinel-database` Secret in namespace `sentinel` with
`ConnectionStrings__Sentinel`, and a reachable PostgreSQL database supporting
pgvector and the existing startup migrations. RDS choice, TLS, database role
and migration privileges, backups and credentials provisioning remain review
inputs. AI/telemetry endpoints are non-secret `api_configuration` inputs.

Infrastructure must be created and reachable **before** initializing/planning
the workload provider against it. After a separately approved infrastructure
deployment, use `aws eks update-kubeconfig --region REGION --name CLUSTER`,
bootstrap the workload namespace with the same targeted pattern, provision
its Secret externally, then review the workload plan. No AWS plan/apply has
been authorized or performed as part of the local deployment.

Schema validation only (does not create AWS resources):

```powershell
terraform -chdir=deploy/terraform/aws/infrastructure init -backend=false
terraform -chdir=deploy/terraform/aws/infrastructure validate
terraform -chdir=deploy/terraform/aws/workloads init -backend=false
terraform -chdir=deploy/terraform/aws/workloads validate
```

Future deployment automation should explicitly trigger on merging into `QA`;
none is installed in this change.

Reference documentation: [Minikube image loading](https://minikube.sigs.k8s.io/docs/handbook/pushing/),
[Kubernetes probes](https://kubernetes.io/docs/concepts/workloads/pods/probes/),
[Terraform Kubernetes Deployment](https://registry.terraform.io/providers/hashicorp/kubernetes/2.38.0/docs/resources/deployment_v1).
