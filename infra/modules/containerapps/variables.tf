variable "environment" {
  description = "Deployment environment. Must be \"dev\" or \"demo\"."
  type        = string

  validation {
    condition     = contains(["dev", "demo"], var.environment)
    error_message = "environment must be \"dev\" or \"demo\"."
  }
}

variable "location" {
  description = "Azure region. Pinned to North Europe for both environments (ADR-006)."
  type        = string
  default     = "North Europe"
}

variable "resource_group_name" {
  description = "Name of the per-environment resource group this module's resources are created in."
  type        = string
}

# Task E01/F02/US04/T02 (ADR-011): the caller (infra/environments/{dev,demo}
# /main.tf) must pass its OWN environment's modules/identity
# `workload_identity_id` output here -- never a literal, never the other
# environment's -- so the API/worker Container Apps can only ever present
# this environment's own workload identity, and therefore can only ever
# reach this environment's own Key Vault (modules/keyvault's role
# assignment is scoped to the matching `workload_principal_id`).
variable "workload_identity_id" {
  description = "ARM resource ID of this environment's user-assigned managed identity (modules/identity's `workload_identity_id` output). Assigned to the API and worker Container Apps so they can authenticate to this environment's own Key Vault (and other Azure services) without a stored secret."
  type        = string
}

variable "acr_login_server" {
  description = "Login server of this environment's Container Registry (modules/acr login_server). Used in registry {} so API/worker pull with the workload identity, not an admin password."
  type        = string
}

variable "postgres_connection_secret_id" {
  description = "Versionless Key Vault secret ID for the Postgres connection string (modules/keyvault postgres_connection_secret_versionless_id). Container Apps resolve it via the workload identity."
  type        = string
}

variable "storage_connection_secret_id" {
  description = "Versionless Key Vault secret ID for the Storage connection string (modules/keyvault storage_connection_secret_versionless_id). Container Apps resolve it via the workload identity."
  type        = string
}

variable "spa_host_name" {
  description = "Default hostname of this environment's Static Web App (no scheme). Ingress CORS allows https://<this> origin only."
  type        = string
}

variable "container_image" {
  description = "Placeholder container image for the API and worker apps until the first real image is published to the acr module's registry (ADR-005: Consumption profile Container Apps)."
  type        = string
  default     = "mcr.microsoft.com/k8se/quickstart:latest"
}

variable "cpu" {
  description = "vCPU per replica (ADR-005 default: 0.25)."
  type        = number
  default     = 0.25
}

variable "memory" {
  description = "Memory per replica (ADR-005 default: 0.5 GiB)."
  type        = string
  default     = "0.5Gi"
}
