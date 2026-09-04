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

variable "web_redirect_uri" {
  description = "SPA redirect URI for the public-client Entra registration (ADR-010 / ADR-012). The env root passes https://<staticwebapp.default_host_name> from modules/staticwebapp. Origin-only values are stored with a trailing slash (Entra requirement)."
  type        = string
}
