---
id: us-01
type: user-story
parent: feature-02
wave: R2
status: active
---

# us-01-threshold-scheduler — Configurable threshold scheduler + events

## Story

As a **backend engineer**, I want a daily scheduler that fires renewal/cancellation
threshold events, so that owners are alerted before deadlines are missed.

## Acceptance criteria

- [ ] AC-1 Threshold windows 365/270/180/120/90/60/30 days, configurable.
- [ ] AC-2 Emits `renewal.approaching` events creating alerts (spec App B).
- [ ] AC-3 Scheduler recomputes when a contract/term is corrected.

## Definition of done

- [ ] `dotnet test` (integration) proves a threshold event fires and an alert is created.
- [ ] honours ADR-002, ADR-003.

## Dependencies

| Depends on | Why |
|------------|-----|
| us-01 (feature-01) | deadlines must exist first |

## Architecture decisions in force

- ADR-002 (worker), ADR-003 (PostgreSQL).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Threshold scheduler + renewal.approaching event | M | phase-21 |
| task-02 | Alert creation + re-compute on correction | M | phase-22 |

## Council decisions carried into this story

Configurable thresholds; events per spec App B; recompute on correction.

## Open questions

- none.
