# Task E01/F02/US02/T02 (dev-outputs-verify): exposes the "Service Bus
# namespace FQDN" resource id/endpoint ADR-005's Concrete-services table
# calls for. azurerm_servicebus_namespace has no exported fqdn/endpoint
# attribute, so this is the namespace name plus Azure public cloud's fixed,
# documented Service Bus DNS suffix -- not a provider-computed value, but
# not a guess either (no sovereign-cloud variant is in scope for V1).
output "id" {
  description = "Azure resource ID of the Service Bus namespace."
  value       = azurerm_servicebus_namespace.this.id
}

output "name" {
  description = "Name of the Service Bus namespace."
  value       = azurerm_servicebus_namespace.this.name
}

output "fqdn" {
  description = "Fully qualified domain name of the Service Bus namespace (Azure public-cloud DNS suffix)."
  value       = "${azurerm_servicebus_namespace.this.name}.servicebus.windows.net"
}
