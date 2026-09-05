---
id: us-01
type: user-story
parent: feature-03
wave: 6
status: active
---

# us-01-signin-workspace-picker — Sign-in + workspace picker

## Story

As a **Procurement/Admin user**, I want to sign in with Entra and pick a
workspace, so I land in the right tenant on `demo`.

## Acceptance criteria

- [ ] AC-1 Sign-in → workspace list (contract count, currency/region, role tag) + "Create a new workspace".
- [ ] AC-2 Redirect state (spinner) between sign-in and picker.
- [ ] AC-3 Wired to real `config.json` (not localhost-only).

## Definition of done

- [ ] Sign-in → picker flow works in the browser on `demo`.
- [ ] honours ADR-012, ADR-018, ADR-020 (screen 1).

## Dependencies

| Depends on | Why |
|------------|-----|
| feature-01 (regen client) | Entra + workspace API |
| E01/F07 OIDC shell (assumed) | auth base |

## Architecture decisions in force

- ADR-012 (OIDC PKCE), ADR-018 (/signin).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Sign-in + workspace picker screen | M | phase-03 |

## Council decisions carried into this story

Two roles only; workspace picker is the landing surface.
