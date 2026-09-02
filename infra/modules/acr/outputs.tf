# Task E01/F02/US02/T02 (dev-outputs-verify): exposes the "ACR login
# server" resource id/endpoint ADR-005's Concrete-services table calls for.
output "id" {
  description = "Azure resource ID of the Container Registry."
  value       = azurerm_container_registry.this.id
}

output "login_server" {
  description = "Login server URL used to push/pull images (docker login / az acr login target)."
  value       = azurerm_container_registry.this.login_server
}
