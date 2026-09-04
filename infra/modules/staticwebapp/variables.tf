variable "environment" {
  description = "Deployment environment. Must be \"dev\" or \"demo\"."
  type        = string

  validation {
    condition     = contains(["dev", "demo"], var.environment)
    error_message = "environment must be \"dev\" or \"demo\"."
  }
}

variable "location" {
  description = "Azure region for the Static Web App resource (managed Functions / staging). Not North Europe: Microsoft.Web/staticSites is not offered there. Default West US 2 is the SWA platform default."
  type        = string
  default     = "West US 2"
}

variable "resource_group_name" {
  description = "Name of the per-environment resource group this module's resources are created in."
  type        = string
}
