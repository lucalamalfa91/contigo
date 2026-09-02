---
id: us-01
type: user-story
parent: feature-03
wave: R3
status: active
---

# us-01-savings-kpis — Savings KPIs + opportunity list API

## Story

As a **Procurement** user, I want the procurement homepage KPIs and opportunity list,
so that I can see the savings picture at a glance.

## Acceptance criteria

- [ ] AC-1 KPIs: annual spend analyzed, savings identified/realized/in-progress, contracts analyzed, upcoming renewals.
- [ ] AC-2 `GET /api/savings` returns the opportunity list with tenant scoping.
- [ ] AC-3 Returns provenance + confidence, never fabricated precision.

## Definition of done

- [ ] `dotnet test` verifies KPI aggregation + opportunity list + tenant scoping.
- [ ] honours ADR-002, ADR-009.

## Dependencies

| Depends on | Why |
|------------|-----|
| feature-02 (savings engine) | KPIs read opportunities |

## Architecture decisions in force

- ADR-002 (Savings API), ADR-009 (RLS).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | KPI aggregation endpoint | M | phase-28 |
| task-02 | Opportunity list + tenant scoping | M | phase-29 |

## Council decisions carried into this story

KPIs per spec §10.1; server-side tenancy.

## Open questions

- none.
