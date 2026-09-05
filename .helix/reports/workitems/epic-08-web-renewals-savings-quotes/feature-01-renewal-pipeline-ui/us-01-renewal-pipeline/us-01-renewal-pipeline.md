---
id: us-01
type: user-story
parent: feature-01
wave: 8
status: active
---

# us-01-renewal-pipeline — Renewal pipeline + insight card + action

## Story

As a **Procurement user**, I want the renewal pipeline with an insight card and
actions, so I don't miss material renewal windows (spec §16 R2).

## Acceptance criteria

- [ ] AC-1 Threshold strip 0–30 … 270–365d with counts (click = filter).
- [ ] AC-2 Table: Score · Supplier · contract · Annual spend · Renews in · Cancel by · Status.
- [ ] AC-3 Insight card (§9.3 fields) + actions (Start negotiation / Assign to me / Snooze) → confirmation + Contract 360 link.
- [ ] AC-4 Deadline ≤45d styled (accent-700, weight 600); states: populated/empty/loading/error/no-window.

## Definition of done

- [ ] Renewal pipeline renders in browser on `demo`, matching `inputs/design/prototypes/screens.md` (8) + `day1-demo.html`.
- [ ] honours ADR-020 (screen 8), ADR-018 (/renewals), ADR-019 (urgency).

## Dependencies

| Depends on | Why |
|------------|-----|
| E03 renewal API (assumed) | pipeline + insight |
| epic-07 contract-360 (Renewal tab) | cross-link |

## Architecture decisions in force

- ADR-020 (screen 8), ADR-018 (/renewals), ADR-019.

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Renewal pipeline + insight + action UI | L | phase-12 |

## Council decisions carried into this story

Action creates an opportunity visible on Home; insight is a card, not a raw JSON dump.
