# Exposed for task E01/F02/US03/T02 (demo-isolation-check): comparing
# this output against the `dev` root's own `resource_group_name` is how
# a later automated check proves "rg-contigo-demo" is never
# "rg-contigo-dev" (ADR-016). scripts/terraform_env_roots_scan.py only
# asserts this file exists (AC-4); it does not read output values.
output "resource_group_name" {
  description = "Name of the demo resource group every module in this root deploys into."
  value       = azurerm_resource_group.this.name
}

output "location" {
  description = "Azure region this environment is deployed to."
  value       = var.location
}

output "static_web_app_name" {
  description = "Name of the demo Static Web App (swa-contigo-demo); web.yml composes this."
  value       = module.staticwebapp.name
}

output "static_web_app_hostname" {
  description = "Default hostname of the demo Static Web App (SPA origin / OIDC redirect)."
  value       = module.staticwebapp.default_host_name
}
