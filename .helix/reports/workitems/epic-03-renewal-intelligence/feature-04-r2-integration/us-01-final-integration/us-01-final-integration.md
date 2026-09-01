---
id: us-01
type: user-story
parent: feature-04
wave: R2
status: active
---

# us-01-final-integration — R2 Renewal Intelligence integration

## Story

As a **product owner**, I want the R2 definition of success proven end-to-end, so
that "Procurement does not miss material renewal windows".

## Acceptance criteria

- [ ] AC-1 Deterministic renewal/cancellation for every active contract (where data exists).
- [ ] AC-2 Threshold events fire and recommendations do not invent dates.
- [ ] AC-3 Pipeline + insight card work on `demo` with tenant isolation.

## Definition of done

- [ ] `dotnet test` (integration) runs the R2 path on fixtures; `demo` smoke documented.

## Dependencies

| Depends on | Why |
|------------|-----|
| every R2 leaf artifact | proves the wave |

## Architecture decisions in force

- ADR-002, ADR-003, ADR-009, ADR-016.

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | R2 end-to-end integration test | L | phase-23 |

## Council decisions carried into this story

R2 success = deterministic dates + alerts + prioritized pipeline.

## Open questions

- none.
