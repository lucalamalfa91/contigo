---
id: us-02
type: user-story
parent: feature-03
wave: R0
status: active
---

# us-02-per-folder-workflows — Per-folder path-filtered CI/CD workflows

## Story

As a **delivery engineer**, I want per-folder GitHub Actions workflows for `infra/`,
`backend/`, `web/`, `mobile/` triggered by path filters and deploying to `dev` on
merge to `main`, so that only the touched deployable is built and released.

## Acceptance criteria

- [ ] AC-1 A workflow for each deployable folder (`infra`, `backend`, `web`, `mobile`) with matching path filters.
- [ ] AC-2 `dev` deploy triggers on merge to `main`.
- [ ] AC-3 `mobile` lane is non-blocking (its failure does not block promotion).

## Definition of done

- [ ] Workflow files present; path filters correct; `mobile` marked non-blocking.

## Dependencies

| Depends on | Why |
|------------|-----|
| us-01 (feature-03) | workflows use the OIDC auth step |

## Architecture decisions in force

- ADR-014 (per-folder deploy, path filters, trunk-based `main`→`dev`).
- ADR-015 (auth step).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Author per-folder workflows + `dev` triggers + non-blocking mobile | L | phase-5 |

## Council decisions carried into this story

Path-filtered jobs per folder; `dev` on `main` merge; mobile non-blocking (ADR-013).

## Open questions

- none
