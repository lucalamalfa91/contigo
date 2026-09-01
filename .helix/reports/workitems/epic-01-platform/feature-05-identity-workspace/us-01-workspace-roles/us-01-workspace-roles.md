---
id: us-01
type: user-story
parent: feature-05
wave: R0
status: active
---

# us-01-workspace-roles — Workspace + roles + membership

## Story

As a **Workspace Admin**, I want to create a workspace and invite users with roles,
so that tenancy and authorization are enforced from day one.

## Acceptance criteria

- [ ] AC-1 Workspace, User, Role, Membership carry `tenant_id` and are RLS-guarded.
- [ ] AC-2 `/api/workspaces` and `/api/users` enforce server-side tenant scoping.
- [ ] AC-3 Role assignment resolves from OIDC claims (Admin/Procurement/Legal/Finance/Read-only).

## Definition of done

- [ ] `dotnet test` proves workspace isolation and role resolution.
- [ ] honours ADR-009, ADR-010, ADR-003.

## Dependencies

| Depends on | Why |
|------------|-----|
| feature-04 (RLS) | tenant guard is the backstop |

## Architecture decisions in force

- ADR-009 (RLS), ADR-010 (OIDC), ADR-003 (PostgreSQL).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Workspace/User/Role/Membership + RLS | M | phase-17 |
| task-02 | Workspace invite + OIDC role claims | M | phase-18 |

## Council decisions carried into this story

Roles per spec §3.1; tenant_id on all business objects; OIDC Entra.

## Open questions

- none.
