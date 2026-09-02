variable "location" {
  description = "Azure region. Pinned to West Europe for both environments (ADR-006)."
  type        = string
  default     = "West Europe"
}

variable "environment" {
  description = "Deployment environment for this root (ADR-007: two thin environment roots, one per environment, never sharing state). This is the \"dev\" root and must always stay \"dev\" -- use environments/demo to instantiate \"demo\"."
  type        = string
  default     = "dev"

  validation {
    condition     = var.environment == "dev"
    error_message = "environments/dev must always set environment = \"dev\"; use environments/demo for the demo root."
  }
}
