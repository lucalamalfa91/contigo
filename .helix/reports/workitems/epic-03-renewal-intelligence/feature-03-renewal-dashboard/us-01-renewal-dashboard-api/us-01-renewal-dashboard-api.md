---
id: us-01
type: user-story
parent: feature-03
wave: R2
status: active
---

# us-01-renewal-dashboard-api — Renewal pipeline + insight card API

## Story

As a **Procurement** user, I want the renewal pipeline and insight card, so that I
see upcoming renewals and why each matters.

## Acceptance criteria

- [ ] AC-1 `GET /api/renewals` returns pipeline (supplier/renewal/days/spend/deadline/action).
- [ ] AC-2 Insight card separates facts from recommendations.
- [ ] AC-3 `POST /api/renewals/{id}/action` updates owner/status/action.

## Definition of done

- [ ] `dotnet test` verifies pipeline + insight card fields + action update.
- [ ] honours ADR-002, ADR-009.

## Dependencies

| Depends on | Why |
|------------|-----|
| feature-01 (renewal engine) | pipeline reads computed renewals |

## Architecture decisions in force

- ADR-002 (Renewals API), ADR-009 (RLS).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Renewal pipeline + insight card endpoint | M | phase-22 |
| task-02 | Renewal action update + tenant scoping | M | phase-23 |

## Council decisions carried into this story

Insight card fields per spec §9.3; facts vs recommendations separated.

## Open questions

- none.
