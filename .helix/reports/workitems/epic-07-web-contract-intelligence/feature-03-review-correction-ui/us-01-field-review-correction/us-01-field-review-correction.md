---
id: us-01
type: user-story
parent: feature-03
wave: 7
status: active
---

# us-01-field-review-correction — Field review + correction + evidence

## Story

As a **Procurement user**, I want to review and correct extraction with evidence,
so low-confidence fields are validated before use (spec §7.3).

## Acceptance criteria

- [ ] AC-1 4-col list: Field (critical marker) · Extracted value+source · Confidence tag · Decision.
- [ ] AC-2 Confidence mapping: >95% neutral, 80–95% flag, <80% review.
- [ ] AC-3 Right evidence pane with highlighted passage + correction form + version.
- [ ] AC-4 "Mark as validated" disabled until all <80% fields decided (visible reason).

## Definition of done

- [ ] Review surface works in browser on `demo`, matching `inputs/design/prototypes/screens.md` (6) + `day1-demo.html`.
- [ ] honours ADR-020 (screen 6), ADR-019 (confidence mapping, disabled CTA).

## Dependencies

| Depends on | Why |
|------------|-----|
| E02 correction API (assumed) | correction history |
| feature-02 (contract 360) | review entry point |

## Architecture decisions in force

- ADR-020 (screen 6), ADR-019 (semantic mapping), spec §7.3.

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Field review + correction + evidence UI | L | phase-10 |

## Council decisions carried into this story

Review/correct is required, not optimisable away (spec §7.3).
