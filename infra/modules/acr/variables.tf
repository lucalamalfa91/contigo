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

# Same isolation rule as modules/keyvault: the caller must pass this
# environment's own modules/identity principal -- never the other env's --
# so AcrPull never crosses the dev/demo boundary (ADR-005, ADR-016).
variable "workload_principal_id" {
  description = "Principal (object) ID of this environment's user-assigned managed identity (modules/identity's `workload_principal_id` output). Granted AcrPull on this environment's own registry only."
  type        = string
}
