# modules/acr -- one Basic-tier Container Registry per environment
# (ADR-005: per-env isolation for any deployment-time pull identity, one
# Basic registry per env rather than one shared registry). Admin
# credentials disabled -- pulls use managed identity + RBAC (AcrPull),
# wired in a later task; no admin secret is ever generated.
locals {
  tags = {
    project = "contigo"
    env     = var.environment
  }
}

resource "random_string" "suffix" {
  length  = 6
  special = false
  upper   = false
  numeric = true
}

resource "azurerm_container_registry" "this" {
  name                = "acrcontigo${var.environment}${random_string.suffix.result}"
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = "Basic"
  admin_enabled       = false

  tags = local.tags
}
