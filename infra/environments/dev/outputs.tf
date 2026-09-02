output "resource_group_name" {
  description = "Name of the dev resource group every module in this root deploys into."
  value       = azurerm_resource_group.this.name
}

output "resource_group_id" {
  description = "Full Azure resource ID of the dev resource group."
  value       = azurerm_resource_group.this.id
}

output "location" {
  description = "Azure region this environment is deployed to."
  value       = var.location
}

output "environment" {
  description = "Deployment environment this root instantiates (always \"dev\")."
  value       = local.environment
}

# Per-service resource ids and endpoints (Postgres FQDN, Container Apps API
# ingress FQDN, ACR login server, Key Vault URI, Storage account name,
# Service Bus namespace FQDN, Log Analytics workspace id) are intentionally
# NOT output here yet: none of the nine modules under infra/modules/
# (scaffolded in E01/F02/US01/T01) declare an `output` block, so this root
# has no module attribute to re-export -- e.g. `module.postgres.fqdn` does
# not exist because modules/postgres/ has no outputs.tf. Adding
# infra/modules/<name>/outputs.tf would reach outside this task's "Files to
# create or modify" (task-01-dev-environment-provision.md) and risks
# colliding with sibling in-flight tasks (US-04 entra-keyvault, US-05
# foundry-account, F03 ci-azure-oidc) that also edit infra/modules/ in this
# same wave. Recorded here rather than silently worked around; the
# per-service outputs this task's own row calls for ("resource ids,
# endpoints") are limited to what the root directly owns until a module
# task adds them.
