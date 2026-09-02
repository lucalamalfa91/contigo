# modules/identity -- Entra app registration + user-assigned managed
# identity (ADR-007 module layout). The managed identity is what
# containerapps' API/worker apps use to read Key Vault, Storage, and
# Service Bus at runtime without a stored secret (ADR-007: no secrets in
# Terraform source).
locals {
  tags = {
    project = "contigo"
    env     = var.environment
  }
}

resource "azurerm_user_assigned_identity" "workload" {
  name                = "id-contigo-${var.environment}-workload"
  location            = var.location
  resource_group_name = var.resource_group_name

  tags = local.tags
}

resource "azuread_application" "api" {
  display_name = "contigo-${var.environment}-api"

  # azuread_application tags are a flat list of category strings (Entra's
  # own tag model), not the key/value azurerm resource tags used
  # elsewhere in this module -- this is the closest equivalent for
  # project/env tracking on an Entra object.
  tags = ["project:contigo", "env:${var.environment}"]
}

resource "azuread_service_principal" "api" {
  client_id = azuread_application.api.client_id
}
