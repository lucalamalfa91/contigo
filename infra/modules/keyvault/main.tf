# modules/keyvault -- one Standard-tier Key Vault per environment
# (ADR-005). RBAC authorization (not access-policy) so runtime access is
# granted via role assignment to modules/identity's managed identity in
# a later task -- this scaffold does not yet grant any access itself.
data "azurerm_client_config" "current" {}

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

resource "azurerm_key_vault" "this" {
  name                       = "kv-contigo-${var.environment}-${random_string.suffix.result}"
  location                   = var.location
  resource_group_name        = var.resource_group_name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  rbac_authorization_enabled = true
  purge_protection_enabled   = false
  soft_delete_retention_days = 7

  tags = local.tags
}
