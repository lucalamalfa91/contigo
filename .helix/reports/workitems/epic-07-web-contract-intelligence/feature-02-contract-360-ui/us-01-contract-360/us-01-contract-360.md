---
id: us-01
type: user-story
parent: feature-02
wave: 7
status: active
---

# us-01-contract-360 — Contract 360 header + tabs

## Story

As a **Procurement user**, I want a Contract 360 detail, so I can see contract
facts, recommendation, and risks with evidence and confidence.

## Acceptance criteria

- [ ] AC-1 Header: supplier kicker, name, type/status tags, doc count.
- [ ] AC-2 6-cell fact row (annual spend, TCV, start→end, renewal+cancellation, risk+priority).
- [ ] AC-3 Overview: recommended-action block + 3 drivers + "Needs your attention" + "Top risks".
- [ ] AC-4 10 tabs; facts labelled "Deterministic" vs AI "Recommended action" (never mixed).

## Definition of done

- [ ] Contract 360 renders in browser on `demo`, matching `inputs/design/prototypes/screens.md` (5) + `day1-demo.html`.
- [ ] honours ADR-020 (screen 5), ADR-019 (facts/AI separation).

## Dependencies

| Depends on | Why |
|------------|-----|
| E02 contract-360 API (assumed) | aggregate |
| feature-01 (portfolio) | navigation source |

## Architecture decisions in force

- ADR-020 (screen 5), ADR-018 (/contracts/:id), ADR-019 (facts/AI).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Contract 360 header + tabs UI | L | phase-09 |

## Council decisions carried into this story

AI recommendation lives in its own labelled block, never mixed with deterministic facts.
