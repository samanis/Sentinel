terraform {
  required_version = ">= 1.6, < 2.0"
  required_providers {
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.38"
    }
  }
}

variable "environment" {
  type = string
  validation {
    condition     = contains(["local", "aws"], var.environment)
    error_message = "environment must be local or aws."
  }
}
variable "namespace" {
  type = string
}
variable "api_image" {
  type = string
  validation {
    condition     = can(regex("(:[^/]+|@sha256:[a-f0-9]{64})$", var.api_image)) && !endswith(var.api_image, ":latest")
    error_message = "Supply an explicit version tag or digest, never latest."
  }
}
variable "database_secret_name" {
  description = "Existing Secret; never read by Terraform. Keys: ConnectionStrings__Sentinel and (local only) POSTGRES_PASSWORD."
  type        = string
  default     = "sentinel-database"
}
variable "enable_hpa" {
  type    = bool
  default = false
}
variable "storage_class_name" {
  type     = string
  default  = null
  nullable = true
}
variable "api_configuration" {
  description = "Non-secret settings only. Optional telemetry/model endpoints can be supplied here."
  type        = map(string)
  default     = {}
  validation {
    condition     = alltrue([for key in keys(var.api_configuration) : !can(regex("(?i)(password|secret|apikey|connectionstrings)", key))])
    error_message = "Credentials must use the externally managed database Secret, not Terraform configuration."
  }
}
