# Task E01/F02/US01/T01 (ADR-007: module layout, remote state per
# environment, no secrets in Terraform source). Single source of truth for
# the Terraform core version and every provider used anywhere under
# infra/. Terraform has no cross-directory file include, so each real root
# module (infra/environments/dev/main.tf, infra/environments/demo/main.tf)
# carries a `terraform` block that mirrors these exact constraints -- keep
# all three in lockstep when a pin changes here.
terraform {
  required_version = ">= 1.8.0, < 2.0.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 3.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
}
