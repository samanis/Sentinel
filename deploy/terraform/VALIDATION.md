# Local deployment validation

Executed September 23, 2026, against the existing Windows 11 developer
environment and Minikube context. No cluster was installed or replaced.

## Verified

- Terraform 1.14.3, Kubernetes provider 2.38.0; existing Minikube node Ready,
  Kubernetes v1.37.0, containerd 2.3.4, default storage class `standard`.
- No applicable repository or ancestor `AGENTS.md` found.
- API build: zero warnings/errors. All 92 solution tests passed (68 Sentinel,
  21 Order API, 3 Telemetry Generator).
- Terraform format and schema validation passed for the local root and both
  AWS review roots. AWS provider locked to 6.66.0.
- Docker image built from `src/Sentinel.Api/Dockerfile` and loaded into the
  existing Docker-driver Minikube node with the scripted containerd fallback.
  The installed image is
  `sentinel-api:e79fa4daa1df-20260923195056-13c581`.
- Namespace plan inspected: one addition, no updates/deletions. Applied.
- Full local workload plan inspected: five additions, no updates/deletions.
  Applied two Deployments, two ClusterIP Services and one PVC.
- Database Secret generated outside Terraform, through stdin. No Secret
  resource/data source, password or connection-string value in Terraform.
- Both Deployments have one available replica; both Pods Running/Ready with
  zero restarts. Services expose internal ports 8080 and 5432. The 10Gi
  ReadWriteOnce PostgreSQL PVC is Bound.
- API startup/liveness paths `/health` and readiness path `/health/ready`
  inspected from the live Deployment.
- HTTP `GET /`, `GET /health`, `GET /health/ready`: 200 via temporary
  localhost port-forward on 15156.
- Incident created and read back:
  `5a11bfa5-cd07-4b08-aa20-291ac418e4f0`, service
  `sentinel-deployment-smoke`. The record remains as verification evidence.
- PostgreSQL scaled to zero: API liveness remained 200; readiness returned 503;
  Kubernetes marked the API Pod NotReady.
- PostgreSQL restored to one replica: readiness recovered, API restart count
  unchanged, and the incident survived database Pod replacement.
- SQL confirmed `vector` extension version 0.8.5.
- Final full Terraform plan returned exit code 0: no changes/drift.
- Verification's temporary port-forward was stopped; workloads remain running.

## Deliberately not deployed or not verified

- Metrics-server is absent; the optional HPA was not enabled, so autoscaling
  behavior/load testing remains unverified.
- Ollama, worker, RAG API and observability stack were not deployed in this
  stage. No claims of end-to-end model inference, embeddings, telemetry
  collection or background ingestion validation. Missing Ollama sizing/version
  inputs and later prerequisites are in the runbook.
- AWS roots were initialized only for schema validation. No AWS plan/apply,
  infrastructure creation, database provisioning or cloud integration test.
- No frontend, automated deployment or self-hosted runner was added.
- Full destroy was not executed; PostgreSQL is intentionally protected by
  `prevent_destroy`. PVC persistence was tested through Pod replacement, not
  host/cluster loss. Backup/restore remains outside this first deployment.

## Issues resolved during execution

- Sandbox access could not read kubeconfig or connect to Docker; authorized
  commands ran with the required access to the existing local services.
- Minikube CLI was not on PATH. The Docker/containerd fallback avoids creating
  or installing any cluster.
- Containerd's transfer service could not read the archive from `/tmp`;
  importing from `/var/lib` succeeded. The script cleans up its archive.
- Windows PowerShell requires quoted dotted Terraform argument values and
  UTF-8 **without BOM** for generated Terraform JSON. Scripts/runbook were
  corrected for both.

Existing untracked launch-settings files under the sample generator and RAG API
were preserved.
