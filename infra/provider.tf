# Task E01/F02/US01/T01 (ADR-007). Reference copy of the provider
# configuration each environment root carries directly in its own
# main.tf (see infra/versions.tf for why this cannot be a shared
# include). `dev` and `demo` each declare an identical `provider
# "azurerm"` / `provider "azuread"` pair so each remains an
# independently initializable Terraform root against its own HCP
# Terraform workspace.
provider "azurerm" {
  features {}
}

provider "azuread" {}
