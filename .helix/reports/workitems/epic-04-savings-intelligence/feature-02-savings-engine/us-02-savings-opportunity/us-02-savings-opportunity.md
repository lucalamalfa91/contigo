---
id: us-02
type: user-story
parent: feature-02
wave: R3
status: active
---

# us-02-savings-opportunity — SavingsOpportunity workflow + realized outcome

## Story

As a **Procurement** user, I want a trackable SavingsOpportunity with status/owner/
realized outcome, so that identified savings become measurable outcomes.

## Acceptance criteria

- [ ] AC-1 `GET /api/savings` lists opportunities; `PATCH /api/savings/{id}` updates status/owner/realized.
- [ ] AC-2 SavingsOpportunity captures supplier/contract/type/current-spend/estimated-range/confidence/status/owner.
- [ ] AC-3 Realized value is captured and audit-tracked (App C #9).

## Definition of done

- [ ] `dotnet test` proves opportunity lifecycle (identify → approve → realized).
- [ ] honours ADR-002, ADR-003, ADR-009.

## Dependencies

| Depends on | Why |
|------------|-----|
| us-01 (this feature) | opportunity derives from comparison |

## Architecture decisions in force

- ADR-002 (Savings context), ADR-003 (PostgreSQL), ADR-009 (RLS), App C #9.

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | SavingsOpportunity entity + lifecycle API | M | phase-27 |
| task-02 | Realized outcome + audit event | M | phase-28 |

## Council decisions carried into this story

Trackable opportunity with realized outcome; audit from day one.

## Open questions

- none.
