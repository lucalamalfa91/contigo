# modules/containerapps -- Container Apps Environment + the API and
# worker Container Apps (ADR-007 module layout; ADR-005: Consumption
# profile, min replicas = 0, 0.25 vCPU / 0.5 GiB default per replica).
# Self-contained at this scaffold stage: it does not yet take
# modules/monitor's Log Analytics workspace id as an input -- a later
# task wires that.
locals {
  tags = {
    project = "contigo"
    env     = var.environment
  }
}

resource "azurerm_container_app_environment" "this" {
  name                = "cae-contigo-${var.environment}"
  location            = var.location
  resource_group_name = var.resource_group_name

  tags = local.tags
}

resource "azurerm_container_app" "api" {
  name                         = "ca-contigo-${var.environment}-api"
  container_app_environment_id = azurerm_container_app_environment.this.id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"

  # Task E01/F02/US04/T02 (ADR-011): this environment's OWN workload
  # identity only (var.workload_identity_id, wired from this same root's
  # module.identity -- see infra/environments/{dev,demo}/main.tf) -- never
  # a literal, never the other environment's. This is what lets the API
  # actually exercise modules/keyvault's "Key Vault Secrets User" grant on
  # this environment's own vault at runtime, with no stored secret.
  identity {
    type         = "UserAssigned"
    identity_ids = [var.workload_identity_id]
  }

  template {
    min_replicas = 0
    max_replicas = 1

    container {
      name   = "api"
      image  = var.container_image
      cpu    = var.cpu
      memory = var.memory
    }
  }

  ingress {
    external_enabled = true
    target_port      = 8080

    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }

  tags = local.tags
}

resource "azurerm_container_app" "worker" {
  name                         = "ca-contigo-${var.environment}-worker"
  container_app_environment_id = azurerm_container_app_environment.this.id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"

  # Task E01/F02/US04/T02 (ADR-011): same identity as the "api" app above
  # -- this environment's own workload identity only -- so the worker can
  # also reach only this environment's own Key Vault, never the other's.
  identity {
    type         = "UserAssigned"
    identity_ids = [var.workload_identity_id]
  }

  template {
    min_replicas = 0
    max_replicas = 1

    container {
      name   = "worker"
      image  = var.container_image
      cpu    = var.cpu
      memory = var.memory
    }
  }

  tags = local.tags
}
