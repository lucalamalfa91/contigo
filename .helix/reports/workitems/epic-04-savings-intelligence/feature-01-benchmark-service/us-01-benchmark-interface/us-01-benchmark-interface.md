---
id: us-01
type: user-story
parent: feature-01
wave: R3
status: active
---

# us-01-benchmark-interface — Benchmark interface + normalized contract

## Story

As a **backend engineer**, I want the Benchmark Service interface with a normalized
getBenchmark contract, so that business modules never depend on a provider schema.

## Acceptance criteria

- [ ] AC-1 `getBenchmark(supplier, product, sku, geography, quantity, term, currency, purchase_date)` returns normalized P25/P50/P75 + metric/currency/confidence/source/updated/comparison.
- [ ] AC-2 Domain modules depend only on the interface (never a provider SDK/API).
- [ ] AC-3 Matching uses more than supplier name (App C #3).

## Definition of done

- [ ] `dotnet test` proves the interface + no provider SDK referenced in domain code.
- [ ] honours ADR-001, ADR-002, App C #3.

## Dependencies

| Depends on | Why |
|------------|-----|
| E01 (backend solution) | Benchmark context exists |

## Architecture decisions in force

- ADR-001 (fixture adapter), ADR-002 (Benchmark context), App C #3.

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Benchmark interface + normalized DTO | M | phase-24 |
| task-02 | Adapter registry + provider SDK isolation | M | phase-25 |

## Council decisions carried into this story

Normalized internal contract per spec §10.3; replaceable adapter.

## Open questions

- none.
