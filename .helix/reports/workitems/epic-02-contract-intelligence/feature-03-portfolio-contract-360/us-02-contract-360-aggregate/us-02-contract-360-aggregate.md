---
id: us-02
type: user-story
parent: feature-03
wave: R1
status: active
---

# us-02-contract-360-aggregate — Contract 360 aggregate

## Story

As a **Procurement** user, I want a Contract 360 view (header + tabs: overview,
commercials, products, clauses, obligations, risks, documents, benchmark, renewal,
activity), so that I can see everything about one contract in one place.

## Acceptance criteria

- [ ] AC-1 `GET /api/contracts/{id}` returns the 360 aggregate (header + tab data).
- [ ] AC-2 Commercials/products read from StructuredContracts + line items; clauses/obligations/risks from extracted facts.
- [ ] AC-3 Authorization filter applies (default tenant scoping).

## Definition of done

- [ ] `dotnet test` assembles the 360 aggregate and checks tenant scoping.
- [ ] honours ADR-002, ADR-009.

## Dependencies

| Depends on | Why |
|------------|-----|
| us-01 (this feature) | 360 reuses portfolio endpoint |

## Architecture decisions in force

- ADR-002, ADR-009.

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Contract 360 aggregate endpoint | M | phase-15 |

## Council decisions carried into this story

360 tabs per spec §8.2.

## Open questions

- none.

## Task-count note

400 is the benchmark/activity tabs placeholder (R3/R4); they read only validated data and return empty until later waves.
