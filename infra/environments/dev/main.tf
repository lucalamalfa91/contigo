# Task E01/F02/US01/T01 (ADR-007: two thin environment roots instantiate
# the shared module library; this one is "dev"). See infra/versions.tf
# and infra/provider.tf for why the terraform{}/provider{} blocks below
# are duplicated here rather than shared -- Terraform has no
# cross-directory include, so each root carries its own copy in
# lockstep.
terraform {
  required_version = ">= 1.8.0, < 2.0.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 3.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
}

provider "azurerm" {
  features {}
}

provider "azuread" {}

# Task E01/F02/US02/T01 (instantiate the dev environment): promote
# `environment` from a bare string literal to var.environment (declared in
# variables.tf, defaulted+locked to "dev") so this root carries "dev
# variables (location, env)" per this task's file scope, instead of a
# hardcoded local.
locals {
  environment = var.environment
}

resource "azurerm_resource_group" "this" {
  name     = "rg-contigo-${local.environment}"
  location = var.location

  tags = {
    project = "contigo"
    env     = local.environment
  }
}

module "network" {
  source = "../../modules/network"

  environment         = local.environment
  location            = var.location
  resource_group_name = azurerm_resource_group.this.name
}

module "identity" {
  source = "../../modules/identity"

  environment         = local.environment
  location            = var.location
  resource_group_name = azurerm_resource_group.this.name
}

module "postgres" {
  source = "../../modules/postgres"

  environment         = local.environment
  location            = var.location
  resource_group_name = azurerm_resource_group.this.name
  # ADR-005: Burstable, smallest tier (the module already enables the
  # pgvector/VECTOR extension unconditionally) -- pinned explicitly here so
  # the dev SKU named in this task's coding objective is visible at the env
  # root rather than resting on the module's (identical) default.
  sku_name = "B_Standard_B1ms"
}

module "storage" {
  source = "../../modules/storage"

  environment         = local.environment
  location            = var.location
  resource_group_name = azurerm_resource_group.this.name
}

module "servicebus" {
  source = "../../modules/servicebus"

  environment         = local.environment
  location            = var.location
  resource_group_name = azurerm_resource_group.this.name
}

module "containerapps" {
  source = "../../modules/containerapps"

  environment         = local.environment
  location            = var.location
  resource_group_name = azurerm_resource_group.this.name
}

module "keyvault" {
  source = "../../modules/keyvault"

  environment         = local.environment
  location            = var.location
  resource_group_name = azurerm_resource_group.this.name
}

module "acr" {
  source = "../../modules/acr"

  environment         = local.environment
  location            = var.location
  resource_group_name = azurerm_resource_group.this.name
}

module "monitor" {
  source = "../../modules/monitor"

  environment         = local.environment
  location            = var.location
  resource_group_name = azurerm_resource_group.this.name
  # ADR-005: Pay-As-You-Go Log Analytics with a daily ingestion cap so an
  # idle dev environment cannot run up a log bill; pinned explicitly (the
  # module default is already 1) so the cap is visible at the env root.
  daily_quota_gb = 1
}
