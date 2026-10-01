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
variable "eks_context" { type = string }
variable "api_image" {
  description = "Previously pushed ECR image with an immutable tag or digest."
  type        = string
}
variable "enable_hpa" {
  type    = bool
  default = false
}
variable "api_configuration" {
  type    = map(string)
  default = {}
}

provider "kubernetes" {
  config_path    = pathexpand(var.kube_config_path)
  config_context = var.eks_context
}

module "sentinel" {
  source            = "../../modules/sentinel"
  environment       = "aws"
  namespace         = "sentinel"
  api_image         = var.api_image
  enable_hpa        = var.enable_hpa
  api_configuration = var.api_configuration
}

output "namespace" { value = module.sentinel.namespace }
