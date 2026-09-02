---
id: us-03
type: user-story
parent: feature-03
wave: R0
status: active
---

# us-03-promotion-dev-demo — Tag + `demo` environment promotion

## Story

As a **delivery engineer**, I want `demo` deployment triggered only by a `demo-v*`
tag on `main` gated by a `demo` GitHub Environment with required reviewers, so
promotion is explicit, isolated, and never an accidental copy of every `dev` deploy.

## Acceptance criteria

- [ ] AC-1 A `demo-v*` tag on `main` triggers the `demo` deploy workflow.
- [ ] AC-2 A `demo` GitHub Environment with required reviewers gates the deploy.
- [ ] AC-3 Promotion moves code/artifacts only — never database, storage, or secrets.

## Definition of done

- [ ] The `demo` workflow exists, tag-triggered, environment-gated, with reviewers configured.

## Dependencies

| Depends on | Why |
|------------|-----|
| us-02 (feature-03) | shares per-folder deploy jobs; adds the demo gate |

## Architecture decisions in force

- ADR-016 — tag `demo-v*` + `demo` environment, required reviewers, code-only.

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Author `demo` promotion workflow (tag + environment + reviewers) | M | phase-5 |

## Council decisions carried into this story

Tag `demo-v*`; environment `demo`; required reviewers (product-owner + security-architect). No data-plane copy.

## Open questions

- OQ-DM-001 (Environments availability) — assumption: available, else protected tag fallback.
- OQ-DM-002 (reviewers) — product-owner + security-architect.
