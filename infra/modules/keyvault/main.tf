# modules/keyvault -- one Standard-tier Key Vault per environment
# (ADR-005). RBAC authorization (not access-policy) so runtime access is
# granted via role assignment to modules/identity's managed identity in
# a later task -- see azurerm_role_assignment.workload_secrets_user below
# (task E01/F02/US04/T01), which is that later task.
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

# Task E01/F02/US04/T01 (ADR-011): grants this environment's own API/worker
# managed identity (modules/identity's single shared "workload" identity)
# read access to secrets in this environment's own vault -- and only this
# one, since var.workload_principal_id is always wired from this same
# environment root's own `module.identity` output (see
# infra/environments/{dev,demo}/main.tf), never the other environment's.
#
# This vault has `rbac_authorization_enabled = true` above, so Azure
# ignores legacy `access_policy` blocks entirely; "Key Vault Secrets User"
# is the RBAC-role equivalent of the legacy get+list secret permissions
# ADR-011 calls for. `skip_service_principal_aad_check` guards against the
# well-known AAD replication lag when the principal (the managed identity)
# was itself just created in the same apply.
resource "azurerm_role_assignment" "workload_secrets_user" {
  scope                            = azurerm_key_vault.this.id
  role_definition_name             = "Key Vault Secrets User"
  principal_id                     = var.workload_principal_id
  skip_service_principal_aad_check = true
}
