---
id: us-05
type: user-story
parent: feature-02
wave: R0
status: active
---

# us-05-foundry-account — Azure AI Foundry hub + two projects

## Story

As a **cloud engineer**, I want one Azure AI Foundry hub with two projects
(`contigo-dev`, `contigo-demo`) under a single pay-as-you-go AI services account, so
that `dev`/`demo` model content is logically isolated without double billing.

## Acceptance criteria

- [ ] AC-1 A single Azure AI Foundry hub exists.
- [ ] AC-2 Two projects `contigo-dev` and `contigo-demo` exist; model deployment config is per-project.
- [ ] AC-3 A single pay-as-you-go Azure AI services account backs both (no second subscription).
- [ ] AC-4 Document Intelligence S0 (`prebuilt-read` / `prebuilt-layout`) is available on that account, with a per-project connection for `contigo-dev` and `contigo-demo` (ADR-017).

## Definition of done

- [ ] Terraform/recorded bootstrap declares hub + 2 projects + one AI services account; usage attributable per project.

## Dependencies

| Depends on | Why |
|------------|-----|
| us-04 (feature-02) | gateway authenticates to Foundry via managed identity/Key Vault |

## Architecture decisions in force

- ADR-008 (one hub, two projects, one PAYG AI services account).
- ADR-017 (OCR in V1: Document Intelligence on the same account).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Provision Foundry hub + two projects + one AI services account | M | phase-4 |

## Council decisions carried into this story

One hub, projects `contigo-dev`/`contigo-demo`, single PAYG Azure AI services account including Document Intelligence S0 for V1 OCR (ADR-017). Chat/embed model IDs confirmed later (ADR-004 / CQ-008).

## Open questions

- none
