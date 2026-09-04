# Task E01/F02/US02/T02 (dev-outputs-verify): exposes the "Key Vault URI"
# resource id/endpoint ADR-005's Concrete-services table calls for -- apps
# read secrets at runtime via this URI plus their managed identity.
output "id" {
  description = "Azure resource ID of the Key Vault."
  value       = azurerm_key_vault.this.id
}

output "vault_uri" {
  description = "URI of the Key Vault, used by apps to read secrets/keys at runtime."
  value       = azurerm_key_vault.this.vault_uri
}

output "postgres_connection_secret_versionless_id" {
  description = "Versionless Key Vault secret ID for postgres-connection, consumed by Container Apps secret { key_vault_secret_id }."
  value       = azurerm_key_vault_secret.postgres_connection.versionless_id
  sensitive   = true
}

output "storage_connection_secret_versionless_id" {
  description = "Versionless Key Vault secret ID for storage-connection, consumed by Container Apps secret { key_vault_secret_id }."
  value       = azurerm_key_vault_secret.storage_connection.versionless_id
  sensitive   = true
}
