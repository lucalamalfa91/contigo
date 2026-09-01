---
id: us-01
type: user-story
parent: feature-01
wave: R2
status: active
---

# us-01-deterministic-dates — Deterministic renewal + cancellation deadlines

## Story

As a **backend engineer**, I want renewal dates and cancellation deadlines computed
deterministically from validated structured data, so that dates are never invented by
the LLM.

## Acceptance criteria

- [ ] AC-1 Renewal date + cancellation deadline derived from contract terms (code, not LLM).
- [ ] AC-2 Days-until/cancellation-deadline computed per active contract.
- [ ] AC-3 Missing dates return "cannot determine" rather than a fabricated value.

## Definition of done

- [ ] `dotnet test` proves deterministic calculation and abstain-on-missing.
- [ ] honours ADR-002, App C #6.

## Dependencies

| Depends on | Why |
|------------|-----|
| epic-02 (validated contract data) | renewals read validated fields |

## Architecture decisions in force

- ADR-002 (Renewals context), App C #6 (deterministic math).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Renewal date + deadline computation | M | phase-19 |
| task-02 | Renewal opportunity generation + abstain | M | phase-20 |

## Council decisions carried into this story

Deterministic date arithmetic in code; never LLM-reasoned dates.

## Open questions

- none.
