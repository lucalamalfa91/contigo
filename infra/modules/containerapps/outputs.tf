# Task E01/F02/US02/T02 (dev-outputs-verify): exposes the "Container Apps
# API ingress FQDN" resource id/endpoint ADR-005's Concrete-services table
# calls for. The worker Container App has no `ingress` block (ADR-005:
# it is not externally reachable), so only its resource id is exposed.
output "container_app_environment_id" {
  description = "Azure resource ID of the Container Apps Environment."
  value       = azurerm_container_app_environment.this.id
}

output "api_id" {
  description = "Azure resource ID of the API Container App."
  value       = azurerm_container_app.api.id
}

output "api_fqdn" {
  description = "Ingress FQDN of the API Container App (external HTTPS endpoint)."
  value       = azurerm_container_app.api.latest_revision_fqdn
}

output "worker_id" {
  description = "Azure resource ID of the worker Container App (no ingress -- not externally reachable)."
  value       = azurerm_container_app.worker.id
}
