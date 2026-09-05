---
id: us-01
type: user-story
parent: feature-01
wave: 6
status: active
---

# us-01-typescript-client-regen — Regenerate TS API client

## Story

As a **web implementer**, I want the generated TypeScript client to reflect the
current OpenAPI contract, so every later screen consumes E02–E05 endpoints
without hand-written DTOs.

## Acceptance criteria

- [ ] AC-1 TS client regenerated from `web/openapi/` (single contract).
- [ ] AC-2 No hand-written DTOs diverge from the generated types.
- [ ] AC-3 `npm run build` succeeds after regen.

## Definition of done

- [ ] Generated client committed; a build proves it type-checks.
- [ ] honours ADR-012, ADR-014.

## Dependencies

| Depends on | Why |
|------------|-----|
| E02–E05 OpenAPI (assumed on main) | contract source |

## Architecture decisions in force

- ADR-012 (one generated TS client).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Regen TS client from web/openapi | S | phase-01 |

## Council decisions carried into this story

Repeating chore; no divergent DTOs.
