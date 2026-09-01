---
id: us-02
type: user-story
parent: feature-01
wave: R2
status: active
---

# us-02-priority-score — Explainable priority score

## Story

As a **Procurement** user, I want a renewal priority score with component breakdown,
so that I understand why a renewal is ranked where it is.

## Acceptance criteria

- [ ] AC-1 Score = spend weight + time urgency + benchmark opportunity + uplift risk + contract risk.
- [ ] AC-2 Component scores are stored separately (explainable + tunable).
- [ ] AC-3 Benchmark-opportunity component reads the R3 benchmark only when available (else neutral).

## Definition of done

- [ ] `dotnet test` verifies total + per-component scores and their explainability.
- [ ] honours ADR-002.

## Dependencies

| Depends on | Why |
|------------|-----|
| us-01 (this feature) | score needs dates + spend |

## Architecture decisions in force

- ADR-002 (Renewals), ADR-003 (component columns).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Priority score components + total | M | phase-20 |
| task-02 | Explainability query + tunable weights | S | phase-21 |

## Council decisions carried into this story

Priority formula per spec §9.2; components persisted, not computed inline.

## Open questions

- none.
