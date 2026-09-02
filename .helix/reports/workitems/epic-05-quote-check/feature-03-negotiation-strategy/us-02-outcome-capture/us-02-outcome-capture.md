---
id: us-02
type: user-story
parent: feature-03
wave: R4
status: active
---

# us-02-outcome-capture — Negotiation outcome capture

## Story

As a **Procurement** user, I want to record the final negotiated outcome, so that
realized savings are tracked and become proprietary learning data.

## Acceptance criteria

- [ ] AC-1 `POST /api/negotiations/outcomes` records original/target/final/saving/discount/duration/levers.
- [ ] AC-2 Realized savings surface on the savings dashboard (cross-wave).
- [ ] AC-3 Outcome is versioned + audit-tracked (App C #5/#9).

## Definition of done

- [ ] `dotnet test` proves outcome capture + realized-savings propagation.
- [ ] honours ADR-002, ADR-003, App C #5/#9.

## Dependencies

| Depends on | Why |
|------------|-----|
| us-01 (this feature) | outcome references the strategy |

## Architecture decisions in force

- ADR-002 (Quotes), ADR-003 (NegotiationOutcome), App C #5/#9.

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | NegotiationOutcome entity + API | M | phase-34 |
| task-02 | Realized-savings propagation + audit | M | phase-35 |

## Council decisions carried into this story

Outcome captured from day one; permissioned learning data.

## Open questions

- none.
