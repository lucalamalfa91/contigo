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

# Task E01/F02/US05/T01 (foundry-account-provision, ADR-008/ADR-011). The
# same workload identity above is what the AI Gateway (running in the API
# and worker Container Apps) will use to call the ADR-008 Foundry account
# (chat/embed) and its Document Intelligence connections (ADR-017)
# without a stored key. Two things are still missing before that is live,
# both out of this task's file scope: (1) modules/containerapps does not
# yet attach this identity to the Container Apps via `identity {
# identity_ids = [...] }` -- this output is that identity_ids value; (2)
# the Foundry hub/projects/AI services account are portal-recorded, not a
# Terraform resource in V1 (ADR-008), so the RBAC role assignment granting
# this identity access to that account (the Foundry-side counterpart of
# modules/keyvault's workload_secrets_user assignment) is a human Portal
# step keyed off workload_principal_id above, not a resource in this repo.
output "workload_identity_id" {
  description = "Full Azure resource ID of this environment's user-assigned workload identity -- input to modules/containerapps' (future) `identity { identity_ids = [...] }` block and to the portal-recorded Foundry/Document Intelligence role assignment (ADR-008, ADR-011), alongside workload_principal_id above."
  value       = azurerm_user_assigned_identity.workload.id
}
