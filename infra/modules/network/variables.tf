variable "environment" {
  description = "Deployment environment. Must be \"dev\" or \"demo\" (ADR-005/ADR-007 -- dev and demo are the only two environments and never share state)."
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
  description = "Name of the per-environment resource group this module's resources are created in (ADR-005: each environment is a distinct resource group)."
  type        = string
}

variable "address_space" {
  description = "VNet address space."
  type        = list(string)
  default     = ["10.0.0.0/16"]
}
