output "resource_group_name" {
  description = "Name of the dev resource group every module in this root deploys into."
  value       = azurerm_resource_group.this.name
}

output "location" {
  description = "Azure region this environment is deployed to."
  value       = var.location
}
