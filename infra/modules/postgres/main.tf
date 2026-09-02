# modules/postgres -- Azure Database for PostgreSQL Flexible Server with
# pgvector enabled (ADR-005, ADR-007). RLS/tenant_id isolation is a
# schema-level (SQL migration) concern owned by the data-access layer,
# not Terraform -- out of scope here.
#
# ADR-007 forbids secrets in Terraform *source*. The administrator
# password is generated with random_password so no literal secret is
# ever written to a .tf file; it still lands in remote state (HCP
# Terraform), which is expected and is not "source". A later task wires
# this into modules/keyvault for app runtime access.
locals {
  tags = {
    project = "contigo"
    env     = var.environment
  }
}

resource "random_password" "administrator" {
  length      = 32
  special     = true
  min_upper   = 2
  min_lower   = 2
  min_numeric = 2
  min_special = 2
}

resource "azurerm_postgresql_flexible_server" "this" {
  name                   = "psql-contigo-${var.environment}"
  location               = var.location
  resource_group_name    = var.resource_group_name
  version                = var.postgres_version
  sku_name               = var.sku_name
  storage_mb             = var.storage_mb
  administrator_login    = var.administrator_login
  administrator_password = random_password.administrator.result

  # No delegated_subnet_id/private_dns_zone_id yet: this scaffold creates
  # the server on public access (still firewalled -- no rule opens it by
  # default). Private networking through modules/network's postgres
  # subnet is wired in a later task.

  tags = local.tags
}

resource "azurerm_postgresql_flexible_server_configuration" "pgvector" {
  name      = "azure.extensions"
  server_id = azurerm_postgresql_flexible_server.this.id
  value     = "VECTOR"
}
