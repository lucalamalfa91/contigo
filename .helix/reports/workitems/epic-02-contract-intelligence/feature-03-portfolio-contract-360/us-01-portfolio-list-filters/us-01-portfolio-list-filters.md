---
id: us-01
type: user-story
parent: feature-03
wave: R1
status: active
---

# us-01-portfolio-list-filters — Portfolio list + filters

## Story

As a **Procurement** user, I want a portfolio list with supplier/spend/dates/risk/
status columns and filters, so that I can triage contracts quickly.

## Acceptance criteria

- [ ] AC-1 `GET /api/contracts` returns the spec §8.1 columns (supplier, contract, annual spend, start/end, renewal, cancellation deadline, auto-renewal, risk, status).
- [ ] AC-2 Filters by supplier/category/renewal period/spend/status/risk/auto-renewal.
- [ ] AC-3 Server-side tenancy filter returns only the caller's tenant (ADR-009).

## Definition of done

- [ ] `dotnet test` verifies columns + filters + tenant scoping.
- [ ] honours ADR-002, ADR-009.

## Dependencies

| Depends on | Why |
|------------|-----|
| feature-02 (contract schema) | portfolio reads contract entities |

## Architecture decisions in force

- ADR-002 (Documents/Contracts API), ADR-009 (RLS).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Portfolio list endpoint + filters | M | phase-14 |
| task-02 | Tenant-scoped query + pagination | M | phase-15 |

## Council decisions carried into this story

Portfolio columns/filters per spec §8.1; server-side tenancy.

## Open questions

- none.
