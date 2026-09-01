---
id: us-02
type: user-story
parent: feature-06
wave: R0
status: active
---

# us-02-audit-baseline — Audit baseline

## Story

As a **Workspace Admin**, I want an append-only audit log of access and corrections,
so that governance is available from day one.

## Acceptance criteria

- [ ] AC-1 Every module writes audit events via a shared audit abstraction.
- [ ] AC-2 `GET /api/audit` returns authorized, tenant-scoped events.

## Definition of done

- [ ] `dotnet test` proves audit write + authorized query.
- [ ] honours ADR-009, ADR-003.

## Dependencies

| Depends on | Why |
|------------|-----|
| feature-05 (roles) | audit reads identity |

## Architecture decisions in force

- ADR-009 (RLS), ADR-003 (PostgreSQL).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Append-only audit abstraction | M | phase-21 |
| task-02 | Authorized audit query | S | phase-22 |

## Council decisions carried into this story

Audit access + corrections from day one (App C #9).

## Open questions

- none.
