# Task E01/F02/US02/T02 (dev-outputs-verify): exposes the "Log Analytics
# workspace id" resource id/endpoint ADR-005's Concrete-services table
# calls for. `id` is the ARM resource ID; `workspace_id` is the separate
# Workspace/Customer GUID diagnostic settings and agents actually target.
output "id" {
  description = "Azure resource ID of the Log Analytics workspace."
  value       = azurerm_log_analytics_workspace.this.id
}

output "workspace_id" {
  description = "Workspace (Customer) ID of the Log Analytics workspace, used by diagnostic settings/agents."
  value       = azurerm_log_analytics_workspace.this.workspace_id
}
