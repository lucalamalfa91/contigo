---
id: us-02
type: user-story
parent: feature-01
wave: R3
status: active
---

# us-02-fixture-adapter — Fixture adapter + provenance/confidence

## Story

As a **backend engineer**, I want an internal fixture benchmark adapter with
provenance + confidence, so that `demo` shows credible benchmark data without a paid
provider.

## Acceptance criteria

- [ ] AC-1 Fixture adapter returns P25/P50/P75 + confidence + provenance for matched fixtures.
- [ ] AC-2 No paid market API is called (spec / ADR-001).
- [ ] AC-3 Benchmark trust: weak comparables yield "insufficient market data", not a fabricated number.

## Definition of done

- [ ] `dotnet test` proves fixture results, confidence, and abstain-on-weak-match.
- [ ] honours ADR-001, spec §10.4.

## Dependencies

| Depends on | Why |
|------------|-----|
| us-01 (this feature) | adapter implements the interface |

## Architecture decisions in force

- ADR-001 (fixture only), spec §10.4 (trust).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Fixture adapter + benchmark dataset | M | phase-25 |
| task-02 | Confidence + insufficient-data abstain | M | phase-26 |

## Council decisions carried into this story

Fixture adapter for first demo; no paid provider; trust over precision.

## Open questions

- none.
