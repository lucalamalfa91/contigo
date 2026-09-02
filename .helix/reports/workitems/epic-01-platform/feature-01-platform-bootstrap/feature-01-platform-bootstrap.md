---
id: feature-01
type: feature
parent: epic-01
wave: R0
status: active
---

# feature-01-platform-bootstrap — GitHub repo, monorepo, HCP, git flow

## Slice

Bootstrap the delivery substrate: the existing GitHub repository
[`lucalamalfa91/contigo`](https://github.com/lucalamalfa91/contigo) (**public**,
description **Contigo platform**) with `infra/`, `backend/`, `web/`, `mobile/`, `.helix/`
folders, trunk-based protected-`main` git flow, and the two HCP Terraform
workspaces (`contigo-dev`, `contigo-demo`) that hold per-environment remote state.

## User stories

| ID | Title | Wave |
|----|-------|------|
| us-01 | Adopt `lucalamalfa91/contigo` + branch protection (ADR-014) | R0 |
| us-02 | HCP Terraform workspaces (ADR-007) | R0 |

## Architecture decisions in force

- ADR-014 — trunk-based, protected `main`, one monorepo at `lucalamalfa91/contigo`
- ADR-007 — remote state per environment (HCP workspaces `contigo-dev`/`contigo-demo`)

## Target repo

`contigo` — https://github.com/lucalamalfa91/contigo (public)
