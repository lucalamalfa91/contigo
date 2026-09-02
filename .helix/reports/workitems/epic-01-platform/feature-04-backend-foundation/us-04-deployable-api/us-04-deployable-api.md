---
id: us-04
type: user-story
parent: feature-04
wave: R0
status: active
---

# us-04-deployable-api — Deployable API host + worker host

## Story

As a **backend engineer**, I want a thin API host and a thin worker host that compose
the modules and expose a health endpoint, so that the backend is deployable to
`dev`/`demo` Container Apps from day one.

## Acceptance criteria

- [ ] AC-1 API host composes modules via DI/mediator and serves a `/health` endpoint.
- [ ] AC-2 Worker host references the same application services and consumes the queue.
- [ ] AC-3 API and worker container images build and are deployable (Dockerfile).

## Definition of done

- [ ] `docker build` + a smoke `curl /health` succeed; worker image builds.

## Dependencies

| Depends on | Why |
|------------|-----|
| us-01 (feature-04) | hosts compose the module projects |

## Architecture decisions in force

- ADR-002 (thin hosts, shared application services).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Wire API + worker hosts + Dockerfiles + health | M | phase-5 |

## Council decisions carried into this story

API host + worker host as thin composition roots; shared services; Docker images.

## Open questions

- none
