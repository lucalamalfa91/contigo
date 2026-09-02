# ADR-013 — Mobile native stack (web-first, non-blocking)

- **Status**: accepted
- **Date**: 2026-09-01
- **Deciders**: client-architect (draft), council-close
- **Locked citations**: Frontend/mobile "Council decides the stacks" (brief §1); "`mobile/` folder still exists in the monorepo" (brief §9); "Product V1 topology is web-first; native must not block `dev`/`demo`" (brief §9); API "Web and mobile consume the backend API" (brief §1); "No secrets in code, client bundles" (brief §1)

## Context and problem statement

V1 is explicitly web-first (brief §9). The `mobile/` folder must exist, and the mobile stack is council-owned, but **nothing in R0–R4's definition of success or the Day-1 promise depends on shipping a native app or passing a store review** (spec §16, §20). The mobile decision must therefore (a) settle on a concrete, non-throwaway stack, and (b) guarantee it cannot gate the `dev`/`demo` delivery path.

## Decision drivers

- **Do not block `dev`/`demo`** — native must be buildable and aligned, but its absence or store-review status can never delay R0/R1 and the Day-1 demo.
- **Shared API contract** — mobile must consume the exact same OIDC + versioned backend API, reusing the OpenAPI/TypeScript client rather than a divergent DTO surface.
- **One codebase, one folder** — a single `mobile/` codebase that targets iOS + Android without maintaining two native projects.
- **Language/tooling alignment** — same TypeScript family as the web client, for a lean AI-assisted team.

## Considered options

1. **React Native (Expo) + TypeScript** — one TS codebase for iOS + Android, OIDC via PKCE.
2. **Flutter (Dart)** — one codebase, separate language/runtime.
3. **Xamarin/.NET MAUI (C#)** — same language as backend.

## Decision outcome

**Chosen: Option 1 — React Native with the Expo toolchain, TypeScript, consuming the same OIDC (PKCE) API as the web client.**

React Native + Expo keeps mobile in the same TypeScript ecosystem as the web client (ADR-web-stack), reusing the shared OpenAPI-generated client and OIDC PKCE flow, with one codebase for iOS + Android under `mobile/`. Critically, mobile is treated as a **parallel, non-gating lane**: it is scaffolded alongside the platform slice but its CI/CD lane and any store submission are explicitly out of the R0/R1 critical path, so the Day-1 `demo` works entirely from the web client.

### Consequences

- **Good**: no second language/runtime; shares language, types, OIDC flow, and API client with web; one codebase for both platforms; Expo drastically lowers native toolchain friction for a small team.
- **Bad**: mobile still adds a build lane, a native toolchain, and (eventually) store-review risk that must be actively de-prioritized so it does not pull focus from R0/R1; TypeScript-native divergence risk across web vs RN must be managed via the shared contract.
- **Neutral**: native feels/performs differently from a web SPA; acceptable because V1 is web-first.

## Pros and cons of the options

### React Native (Expo) + TypeScript
- Good: shared TS language + shared API/OIDC client with web; Expo EAS simplifies builds; widest ecosystem/community.
- Bad: native toolchain and store submission are still real costs that must be kept off the critical path.

### Flutter (Dart)
- Good: one codebase, strong UI consistency.
- Bad: introduces Dart as a third language; no client-contract sharing with the future web/TS surface; weaker alignment with the web decision.

### .NET MAUI (C#)
- Good: matches backend language.
- Bad: smaller ecosystem/tooling maturity for this team; does not share the web UI language; heavier native onboarding than Expo for a web-first V1.

## Implications for the decomposition

- Scaffold `mobile/` as a single React Native (Expo) + TypeScript app that imports the **same generated OpenAPI/TypeScript client** and OIDC PKCE flow as `web/` — no hand-written duplicate DTOs.
- Add a `mobile/` CI path-filtered lane, but mark it **non-blocking**: its failure or absence must not block `dev`/`demo` promotion or the Day-1 demo.
- No mobile store release is required for any R0–R4 wave or the Day-1 promise; a store release is a future, separately-gated item.
- Mobile reads API base URL + OIDC authority from per-environment config, exactly as `web/` does; no client secret is ever placed in the app/bundle.

## Assumptions

- Expo/React Native supports the OIDC Authorization Code + PKCE flow against Entra ID for public clients.
- Mobile lanes can be configured as non-blocking within the council's git flow / CI (confirm with delivery-manager at council-close).
- A mobile store release is genuinely out of scope for V1 `dev`/`demo` (matches spec §16/§20; no store-release dependency appears there).
