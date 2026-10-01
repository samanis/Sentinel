locals {
  labels = {
    "app.kubernetes.io/part-of"    = "sentinel"
    "app.kubernetes.io/managed-by" = "terraform"
    environment                    = var.environment
  }
  api_labels = merge(local.labels, { "app.kubernetes.io/name" = "sentinel-api" })
  db_labels  = merge(local.labels, { "app.kubernetes.io/name" = "postgres" })
}

resource "kubernetes_namespace_v1" "sentinel" {
  metadata {
    name   = var.namespace
    labels = local.labels
  }
}

resource "kubernetes_persistent_volume_claim_v1" "postgres" {
  count = var.environment == "local" ? 1 : 0
  metadata {
    name      = "postgres-data"
    namespace = kubernetes_namespace_v1.sentinel.metadata[0].name
    labels    = local.db_labels
  }
  spec {
    access_modes       = ["ReadWriteOnce"]
    storage_class_name = var.storage_class_name
    resources {
      requests = { storage = "10Gi" }
    }
  }
  lifecycle {
    prevent_destroy = true
  }
}

resource "kubernetes_deployment_v1" "postgres" {
  count = var.environment == "local" ? 1 : 0
  metadata {
    name      = "postgres"
    namespace = kubernetes_namespace_v1.sentinel.metadata[0].name
    labels    = local.db_labels
  }
  spec {
    replicas = 1
    strategy { type = "Recreate" }
    selector { match_labels = local.db_labels }
    template {
      metadata { labels = local.db_labels }
      spec {
        automount_service_account_token = false
        container {
          name  = "postgres"
          image = "pgvector/pgvector:0.8.5-pg18-bookworm"
          port { container_port = 5432 }
          env {
            name  = "POSTGRES_DB"
            value = "sentinel"
          }
          env {
            name  = "POSTGRES_USER"
            value = "sentinel"
          }
          env {
            name = "POSTGRES_PASSWORD"
            value_from {
              secret_key_ref {
                name = var.database_secret_name
                key  = "POSTGRES_PASSWORD"
              }
            }
          }
          resources {
            requests = { cpu = "100m", memory = "256Mi" }
            limits   = { cpu = "1", memory = "1Gi" }
          }
          volume_mount {
            name       = "data"
            mount_path = "/var/lib/postgresql"
          }
          startup_probe {
            exec { command = ["pg_isready", "-U", "sentinel", "-d", "sentinel"] }
            period_seconds    = 5
            failure_threshold = 60
          }
          readiness_probe {
            exec { command = ["pg_isready", "-U", "sentinel", "-d", "sentinel"] }
            period_seconds  = 5
            timeout_seconds = 3
          }
          liveness_probe {
            exec { command = ["pg_isready", "-U", "sentinel", "-d", "sentinel"] }
            period_seconds    = 10
            timeout_seconds   = 3
            failure_threshold = 6
          }
        }
        volume {
          name = "data"
          persistent_volume_claim {
            claim_name = kubernetes_persistent_volume_claim_v1.postgres[0].metadata[0].name
          }
        }
      }
    }
  }
}

resource "kubernetes_service_v1" "postgres" {
  count = var.environment == "local" ? 1 : 0
  metadata {
    name      = "postgres"
    namespace = kubernetes_namespace_v1.sentinel.metadata[0].name
  }
  spec {
    selector = local.db_labels
    port {
      port        = 5432
      target_port = 5432
    }
  }
}

resource "kubernetes_deployment_v1" "api" {
  metadata {
    name      = "sentinel-api"
    namespace = kubernetes_namespace_v1.sentinel.metadata[0].name
    labels    = local.api_labels
  }
  spec {
    replicas = 1
    selector { match_labels = local.api_labels }
    template {
      metadata { labels = local.api_labels }
      spec {
        automount_service_account_token = false
        container {
          name              = "api"
          image             = var.api_image
          image_pull_policy = "IfNotPresent"
          port {
            name           = "http"
            container_port = 8080
          }
          env {
            name = "ConnectionStrings__Sentinel"
            value_from {
              secret_key_ref {
                name = var.database_secret_name
                key  = "ConnectionStrings__Sentinel"
              }
            }
          }
          dynamic "env" {
            for_each = merge({
              ASPNETCORE_ENVIRONMENT = "Production"
              ASPNETCORE_HTTP_PORTS  = "8080"
              AI__Provider           = "Ollama"
            }, var.api_configuration)
            content {
              name  = env.key
              value = env.value
            }
          }
          resources {
            requests = { cpu = "100m", memory = "256Mi" }
            limits   = { cpu = "1", memory = "512Mi" }
          }
          startup_probe {
            http_get {
              path = "/health"
              port = "http"
            }
            period_seconds    = 5
            failure_threshold = 60
          }
          readiness_probe {
            http_get {
              path = "/health/ready"
              port = "http"
            }
            period_seconds  = 5
            timeout_seconds = 3
          }
          liveness_probe {
            http_get {
              path = "/health"
              port = "http"
            }
            period_seconds    = 10
            timeout_seconds   = 3
            failure_threshold = 3
          }
        }
      }
    }
  }
  depends_on = [kubernetes_deployment_v1.postgres, kubernetes_service_v1.postgres]
  lifecycle {
    ignore_changes = [spec[0].replicas]
  }
}

resource "kubernetes_horizontal_pod_autoscaler_v2" "api" {
  count = var.enable_hpa ? 1 : 0
  metadata {
    name      = "sentinel-api"
    namespace = kubernetes_namespace_v1.sentinel.metadata[0].name
  }
  spec {
    min_replicas = 1
    max_replicas = 3
    scale_target_ref {
      api_version = "apps/v1"
      kind        = "Deployment"
      name        = kubernetes_deployment_v1.api.metadata[0].name
    }
    metric {
      type = "Resource"
      resource {
        name = "cpu"
        target {
          type                = "Utilization"
          average_utilization = 70
        }
      }
    }
  }
}

resource "kubernetes_service_v1" "api" {
  metadata {
    name      = "sentinel-api"
    namespace = kubernetes_namespace_v1.sentinel.metadata[0].name
  }
  spec {
    type     = "ClusterIP"
    selector = local.api_labels
    port {
      name        = "http"
      port        = 8080
      target_port = "http"
    }
  }
}

output "namespace" { value = kubernetes_namespace_v1.sentinel.metadata[0].name }
output "api_service" { value = kubernetes_service_v1.api.metadata[0].name }
