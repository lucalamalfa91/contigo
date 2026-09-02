# Task E01/F02/US04/T01 (entra-keyvault-provision). Non-secret values a
# later task wires into: the API/web/mobile OIDC configuration (ADR-010 --
# public_client_id, api_client_id/api_identifier_uri as audience, and
# issuer), and modules/keyvault's role assignment (ADR-011 --
# workload_principal_id, so this environment's managed identity can be
# granted access to this environment's own vault only).
output "public_client_id" {
  description = "Application (client) ID of the public-client Entra registration web and mobile authenticate with (Authorization Code + PKCE, no secret)."
  value       = azuread_application.public_client.client_id
}

output "api_client_id" {
  description = "Application (client) ID of the API Entra registration -- the default `aud` claim on v2 access tokens issued for this environment's API."
  value       = azuread_application.api.client_id
}

output "api_identifier_uri" {
  description = "App ID URI of the API Entra registration -- the alternate audience/resource identifier a client may request a token for."
  # identifier_uris is a set(string) -- sets have no index, but this
  # module only ever assigns exactly one, so `one(...)` is the correct
  # (and only valid) way to pull that single element back out.
  value = one(azuread_application.api.identifier_uris)
}

output "issuer" {
  description = "OIDC v2 issuer URL for this Entra tenant -- the API validates a token's `iss` claim against this value (ADR-010)."
  value       = "https://login.microsoftonline.com/${data.azuread_client_config.current.tenant_id}/v2.0"
}

output "workload_principal_id" {
  description = "Principal (object) ID of this environment's user-assigned workload identity -- input to modules/keyvault's role assignment so the API/worker can read this environment's own vault only (ADR-011)."
  value       = azurerm_user_assigned_identity.workload.principal_id
}

# Task E01/F02/US04/T02 (keyvault-scope-grants): the ARM resource id of the
# SAME workload identity `workload_principal_id` (above) identifies by
# principal/object id. `azurerm_role_assignment.principal_id` (modules/
# keyvault) and `azurerm_container_app.identity[0].identity_ids`
# (modules/containerapps) take two different shapes of identifier for one
# underlying identity -- a role assignment's principal_id is the AAD
# object id, while attaching a user-assigned identity to a resource needs
# its full ARM resource id. Without this output, modules/containerapps has
# no (non-literal) way to receive the identity it must present at runtime
# to actually exercise modules/keyvault's grant.
output "workload_identity_id" {
  description = "Azure resource ID of this environment's user-assigned workload identity -- input to modules/containerapps so the API/worker Container Apps are assigned this identity (never the other environment's) and can present it at runtime against this environment's own Key Vault grant (ADR-011)."
  value       = azurerm_user_assigned_identity.workload.id
}
