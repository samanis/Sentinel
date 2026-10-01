terraform {
  required_version = ">= 1.6, < 2.0"
  required_providers {
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.38"
    }
  }
}

variable "kube_config_path" {
  type    = string
  default = "~/.kube/config"
}
variable "kube_context" {
  type    = string
  default = "minikube"
}
variable "api_image" { type = string }
variable "enable_hpa" {
  type    = bool
  default = false
}

provider "kubernetes" {
  config_path    = pathexpand(var.kube_config_path)
  config_context = var.kube_context
}

module "sentinel" {
  source             = "../modules/sentinel"
  environment        = "local"
  namespace          = "sentinel-local"
  api_image          = var.api_image
  enable_hpa         = var.enable_hpa
  storage_class_name = "standard"
}

output "namespace" { value = module.sentinel.namespace }
output "api_service" { value = module.sentinel.api_service }
