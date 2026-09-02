# Task E01/F02/US02/T02 (dev-outputs-verify): exposes the "Storage account
# name" resource id/endpoint ADR-005's Concrete-services table calls for,
# plus the blob/queue endpoints backing the "Object storage" and
# "Queue (simple)" rows of that same table.
output "id" {
  description = "Azure resource ID of the Storage Account."
  value       = azurerm_storage_account.this.id
}

output "name" {
  description = "Name of the Storage Account."
  value       = azurerm_storage_account.this.name
}

output "primary_blob_endpoint" {
  description = "Primary Blob service endpoint (contract documents container)."
  value       = azurerm_storage_account.this.primary_blob_endpoint
}

output "primary_queue_endpoint" {
  description = "Primary Queue service endpoint (lightweight inbox/dead-letter queue)."
  value       = azurerm_storage_account.this.primary_queue_endpoint
}
