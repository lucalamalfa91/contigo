---
id: us-01
type: user-story
parent: feature-01
wave: 7
status: active
---

# us-01-portfolio-list-filters — Portfolio list + filters + attention strip

## Story

As a **Procurement user**, I want a filtered portfolio with an attention strip,
so I can see which contracts need attention first.

## Acceptance criteria

- [ ] AC-1 Filter chips (Supplier, Category, Renewal period, Spend, Status, Risk, Auto-renewal).
- [ ] AC-2 Attention strip (Deadlines <45d, Need review, Failed/processing, High risk) click = filter.
- [ ] AC-3 Table sorted by severity → deadline; critical rows tinted + red bar.
- [ ] AC-4 States: loading (skeleton), empty, error (503+retry), no-match-for-filter.

## Definition of done

- [ ] Portfolio renders in browser on `demo`, matching `inputs/design/prototypes/screens.md` (4) + `day1-demo.html`.
- [ ] honours ADR-020 (screen 4), ADR-019 (urgency/table).

## Dependencies

| Depends on | Why |
|------------|-----|
| E02 portfolio API (assumed) | contract rows |
| epic-06 shell + regenerated client | nav + types |

## Architecture decisions in force

- ADR-020 (screen 4), ADR-019 (table/urgency/tag).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Portfolio + filters + attention strip UI | L | phase-08 |

## Council decisions carried into this story

Urgency is worded in column one, not colour-only.
