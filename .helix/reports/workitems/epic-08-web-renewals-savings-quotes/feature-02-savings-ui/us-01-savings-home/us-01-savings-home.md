---
id: us-01
type: user-story
parent: feature-02
wave: 8
status: active
---

# us-01-savings-home — Savings KPIs + opportunities

## Story

As a **Procurement user**, I want a Home with savings KPIs and opportunities,
so I see prioritized savings at a glance (spec §16 R3).

## Acceptance criteria

- [ ] AC-1 6 KPI cells: Annual spend analyzed · Savings identified · Savings realized · Savings in progress · Contracts analyzed · Upcoming renewals.
- [ ] AC-2 Opportunities table: Opportunity · Type · Current spend · Estimated savings · Confidence · Owner · Status · Realized.
- [ ] AC-3 Rows open Contract 360 › Benchmark / Quote check; states incl. benchmark-provider-unreachable (KPIs stale-labelled).

## Definition of done

- [ ] Home renders in browser on `demo`, matching `inputs/design/prototypes/screens.md` (9) + `day1-demo.html`.
- [ ] honours ADR-020 (screen 9), ADR-018 (/ home).

## Dependencies

| Depends on | Why |
|------------|-----|
| E04 savings API (assumed) | KPIs + opportunities |
| feature-01 (renewal) | action-created opportunity |

## Architecture decisions in force

- ADR-020 (screen 9), ADR-018 (/ home), ADR-019.

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Savings KPIs + opportunities UI | M | phase-13 |

## Council decisions carried into this story

Home is the north-star landing (what we bought/pay/when to act/where to save).
