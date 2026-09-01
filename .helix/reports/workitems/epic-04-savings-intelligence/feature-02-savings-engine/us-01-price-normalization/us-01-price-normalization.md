---
id: us-01
type: user-story
parent: feature-02
wave: R3
status: active
---

# us-01-price-normalization — Price normalization + percentile comparison

## Story

As a **Procurement** user, I want current unit prices normalized and compared to
benchmark percentiles, so that I see a target range and saving computed correctly.

## Acceptance criteria

- [ ] AC-1 Normalize current unit price (currency/quantity/term) before comparison.
- [ ] AC-2 Compute percentile, recommended target, and savings range deterministically.
- [ ] AC-3 Show confidence + provenance on the comparison.

## Definition of done

- [ ] `dotnet test` proves deterministic target/savings from a benchmark result.
- [ ] honours ADR-002, ADR-003, App C #6.

## Dependencies

| Depends on | Why |
|------------|-----|
| feature-01 (benchmark) | comparison needs benchmark data |

## Architecture decisions in force

- ADR-002 (Savings), ADR-003, App C #6 (deterministic money).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Price normalization + percentile/target/range | L | phase-26 |
| task-02 | Confidence + provenance propagation | S | phase-27 |

## Council decisions carried into this story

Deterministic money math in code; never LLM-computed savings.

## Open questions

- none.
