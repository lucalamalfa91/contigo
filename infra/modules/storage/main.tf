# modules/storage -- one Storage Account (Blob + Queue) per environment
# (ADR-005). dev and demo each get their own account; never shared.
locals {
  tags = {
    project = "contigo"
    env     = var.environment
  }
}

resource "random_string" "suffix" {
  length  = 6
  special = false
  upper   = false
  numeric = true
}

resource "azurerm_storage_account" "this" {
  name                     = "stcontigo${var.environment}${random_string.suffix.result}"
  location                 = var.location
  resource_group_name      = var.resource_group_name
  account_kind             = "StorageV2"
  account_tier             = "Standard"
  account_replication_type = "LRS"

  tags = local.tags
}

resource "azurerm_storage_container" "documents" {
  name                  = "documents"
  storage_account_id    = azurerm_storage_account.this.id
  container_access_type = "private"
}
