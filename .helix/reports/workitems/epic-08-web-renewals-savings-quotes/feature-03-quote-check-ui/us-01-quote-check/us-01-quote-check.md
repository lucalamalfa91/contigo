---
id: us-01
type: user-story
parent: feature-03
wave: 8
status: active
---

# us-01-quote-check — Quote check stepper + negotiation + outcome

## Story

As a **Procurement user**, I want the quote-check stepper, so a new proposal is
assessed in minutes (spec §16 R4).

## Acceptance criteria

- [ ] AC-1 Stepper Extract → Assessment → Target → Negotiation.
- [ ] AC-2 Extract: line table + benchmark match; unmatched SKU block with manual map + recalculate (assessment blocked until resolved).
- [ ] AC-3 Assessment: 4 numbers + P25/P50/P75 table + provenance card; Target: price ladder + editable target.
- [ ] AC-4 Negotiation: levers with evidence + outcome form → recorded outcome → Home Savings Realized updates.

## Definition of done

- [ ] Quote check renders in browser on `demo`, matching `inputs/design/prototypes/screens.md` (10) + `day1-demo.html`.
- [ ] honours ADR-020 (screen 10), ADR-018 (/quotes/:id).

## Dependencies

| Depends on | Why |
|------------|-----|
| E05 quote API (assumed) | extraction/assessment/strategy |
| epic-06 shell | nav + wiring |

## Architecture decisions in force

- ADR-020 (screen 10), ADR-018 (/quotes/:id), ADR-019 (provenance card).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Quote check stepper + negotiation + outcome UI | L | phase-14 |

## Council decisions carried into this story

Outcome feeds Home Savings Realized (cross-link in Day-1 path).
