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
