---
id: us-03
type: user-story
parent: feature-04
wave: R0
status: active
---

# us-03-tenant-rls — Tenant RLS policies + connection tenant claim

## Story

As a **security engineer**, I want Postgres RLS on every tenant-scoped table with a
per-connection `app.tenant_id` claim and a migration-time check, so a cross-tenant
read is impossible even under developer error.

## Acceptance criteria

- [ ] AC-1 Every tenant-scoped table has a non-null indexed `tenant_id`.
- [ ] AC-2 A `FORCE ROW LEVEL SECURITY` policy keyed to `current_setting('app.tenant_id', true)` is applied per table.
- [ ] AC-3 Data-access layer sets `app.tenant_id` per request/job and clears on connection return; no `BYPASSRLS` in the app path.
- [ ] AC-4 A CI migration check rejects a tenant-scoped table without an RLS policy.

## Definition of done

- [ ] A test proves tenant A cannot read tenant B's rows; migration check runs in CI.

## Dependencies

| Depends on | Why |
|------------|-----|
| us-02 (feature-04) | tables must exist to add RLS |

## Architecture decisions in force

- ADR-009 — RLS on every tenant table, `tenant_id` explicit, RLS as backstop.

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Add RLS + tenant claim + migration check | L | phase-5 |

## Council decisions carried into this story

`tenant_id` on every business table; `FORCE ROW LEVEL SECURITY`; `SET app.tenant_id` per connection; no BYPASSRLS.

## Open questions

- OQ-sec-001 (SKU supports RLS) — assumption: RLS available; forces SKU if not.
