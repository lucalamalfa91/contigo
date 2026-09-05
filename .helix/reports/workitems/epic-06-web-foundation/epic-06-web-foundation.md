---
id: epic-06
type: epic
wave: 6
layer: web
status: active
---

# epic-06-web-foundation — Web foundation (design system + app shell + R0 UI)

## Business capability

Deliver the shared web foundation every later web wave builds on: the Claude
Design design system ported into `web/` as the shared token/component sheet, a
current generated TypeScript client, the sign-in → workspace picker flow, the
224px left-rail app shell with admin/procurement route guards and the global
Ask bar scaffold, and the R0 surfaces (members & roles, document upload,
document-status read-back) — so a procurement user can sign in, pick a
workspace, and ingest documents **in the browser** (spec §16 R0, §20 Day-1
open).

## Product coverage

| Source | Item |
|--------|------|
| spec §16 | R0 — Foundation (definition of success, in-browser) |
| spec §20 | Create workspace + invite Procurement; upload portfolio |
| spec §3.1/§3.2 | workspace, members, roles, multi-tenancy |
| spec §7.1 | document statuses (processing, needs_review, failed) |
| ADR-012 | React + TS + Vite SPA (locked) |
| ADR-018 | IA: left rail, routes /signin, /workspace/members, /documents |
| ADR-019 | design system tokens/components (adopt verbatim) |
| ADR-020 | screens 1, 2, 3 |

## Features

| ID | Title | Wave |
|----|-------|------|
| feature-01 | typescript-client-regen | 6 |
| feature-02 | design-system | 6 |
| feature-03 | app-shell | 6 |
| feature-04 | workspace-members-ui | 6 |
| feature-05 | document-upload-ui | 6 |

## Success looks like

A reviewer signs in on `demo` via Entra, sees the workspace picker, lands in the
left-rail shell, invites a Procurement user, uploads a contract PDF, and reads
the document's processing status in a table — all styled from the shared design
system and wired through the regenerated TypeScript client (no `config.json`
localhost shell).

## Architecture decisions in force

- ADR-012, ADR-018, ADR-019, ADR-020. Consumes `inputs/design/prototypes/design-system.md`, `ia.md`, `screens.md`, `day1-demo.html`.

## Out of scope

- R1–R4 capability UIs (portfolio, Contract 360, review, Ask, renewals, savings, quote check) — later web epics.
- Mobile (ADR-013 stays non-gating scaffold).
- Backend API changes (E01–E05 are treated as done); any thin API gap must be a named, ADR-acknowledged backend task.
