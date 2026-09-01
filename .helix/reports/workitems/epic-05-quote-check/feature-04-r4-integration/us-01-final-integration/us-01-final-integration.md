---
id: us-01
type: user-story
parent: feature-04
wave: R4
status: active
---

# us-01-final-integration — R4 Quote Check integration (Day-1 path)

## Story

As a **product owner**, I want the customer Day-1 path (spec §20) proven end-to-end
on `demo`, so that "a new proposal can be assessed in minutes".

## Acceptance criteria

- [ ] AC-1 Upload quote → line items → benchmark match → market assessment → target range → negotiation strategy.
- [ ] AC-2 User can correct SKU matching before accepting assessment.
- [ ] AC-3 Record final outcome → realized savings tracked.

## Definition of done

- [ ] `dotnet test` (integration) runs the R4 Day-1 path with fixture benchmark; `demo` smoke documented.

## Dependencies

| Depends on | Why |
|------------|-----|
| every R4 leaf artifact | proves the wave |

## Architecture decisions in force

- ADR-001, ADR-002, ADR-003, ADR-009, ADR-016.

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | R4 Day-1 end-to-end integration test | L | phase-35 |

## Council decisions carried into this story

R4 success = quote assessable in minutes on `demo`.

## Open questions

- none.
