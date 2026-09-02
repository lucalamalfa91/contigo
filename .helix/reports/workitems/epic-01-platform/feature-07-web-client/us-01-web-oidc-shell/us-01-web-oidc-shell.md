---
id: us-01
type: user-story
parent: feature-07
wave: R0
status: active
---

# us-01-web-oidc-shell — Web OIDC shell + API client

## Story

As a **Procurement user**, I want to sign in via OIDC and reach the API, so that the
web client is deployable to `dev`/`demo`.

## Acceptance criteria

- [ ] AC-1 SPA authenticates with OIDC Authorization Code + PKCE (no client secret in bundle).
- [ ] AC-2 API base URL / OIDC authority / client_id come from per-env config (not source).
- [ ] AC-3 TypeScript client generated from the single OpenAPI document.

## Definition of done

- [ ] `npm run build` succeeds; `curl` on `/health` via the API client succeeds.
- [ ] honours ADR-012, ADR-010.

## Dependencies

| Depends on | Why |
|------------|-----|
| feature-04 (deployable API) | API origin |
| feature-05 (roles) | OIDC claims |

## Architecture decisions in force

- ADR-012 (React+TS+Vite), ADR-010 (OIDC PKCE).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | React SPA + OIDC PKCE | M | phase-23 |
| task-02 | Generated TS client + health | S | phase-24 |

## Council decisions carried into this story

No secret in bundle; config-not-code; one OpenAPI client.

## Open questions

- none.
