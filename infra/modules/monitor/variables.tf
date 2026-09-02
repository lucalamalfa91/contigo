variable "environment" {
  description = "Deployment environment. Must be \"dev\" or \"demo\"."
  type        = string

  validation {
    condition     = contains(["dev", "demo"], var.environment)
    error_message = "environment must be \"dev\" or \"demo\"."
  }
}

variable "location" {
  description = "Azure region. Pinned to West Europe for both environments (ADR-006)."
  type        = string
  default     = "West Europe"
}

variable "resource_group_name" {
  description = "Name of the per-environment resource group this module's resources are created in."
  type        = string
}

variable "daily_quota_gb" {
  description = "Daily ingestion cap in GB (ADR-005: prevents idle-log runaway cost)."
  type        = number
  default     = 1
}
