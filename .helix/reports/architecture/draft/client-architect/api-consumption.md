# client-architect — API consumption notes (not an ADR)

These notes explain how the two client ADRs consume the backend API. They are constraints on the client lane and inputs to the software-architect (API versioning) and security-architect (OIDC/tenancy) at council-close.

## 1. One versioned contract, consumed by both clients

- The backend exposes a single versioned OpenAPI document. Both `web/` and `mobile/` generate a TypeScript client from **the same** OpenAPI output. There is no hand-written divergent DTO or endpoint list on the client side.
- Versioning is carried in the URL path (e.g. `/v1/...`) and/or an explicit `Accept`/header, decided by the software-architect; the clients honor exactly one active version per deployment so a `dev`/`demo` mismatch fails loudly, not silently.

## 2. Authentication and authorization

- Both clients authenticate via **OIDC Authorization Code + PKCE** against Entra ID. They are **public clients**: they carry only a `client_id` and redirect URI.
- **No client secret** is placed in source, the static bundle, or the mobile app (locked: "No secrets in code, client bundles"). Tokens are obtained interactively by the user agent / OS browser and sent to the API as `Authorization: Bearer`.
- The API is the sole authority for what a token's user/tenant may access. Clients render only what the API returns; the RAG isolation rule (auth-before-retrieval, brief §10) is enforced server-side and is invisible to, and never re-implemented by, the client.
- Refresh/session handling uses the provider's standard flow (MSAL) — the client never stores a refresh secret beyond what MSAL secures on-device.

## 3. Config, not code

- Each client reads three runtime values from per-environment injection (Static Web App app settings / expo config), **never** from source:
  - API base URL (per environment `dev` vs `demo`)
  - OIDC authority / tenant
  - OIDC `client_id` (public) + redirect URI
- This lets a single build deploy to both environments with only config differing, and keeps region/env coupling out of the bundles.

## 4. Cost and hosting implications to hand off

- Web is hosted statically (Static Web Apps free tier, ADR-web-stack). No client-side server process to bill.
- Mobile has no hosting cost; it is a non-gating build lane (ADR-mobile-stack).
- The only client-adjacent cost is Azure Static Web Apps (free) and standard Entra ID OIDC; both under the cost guideline — confirm SKU/region availability with cloud-architect.

## 5. Open questions to resolve at council-close

These are recorded in `reports/open-questions.md` (assumptions in force, non-blocking):

- Static Web Apps free-tier availability in the chosen region.
- Entra ID PKCE public-client support for `dev`/`demo` tenants.
- Whether a BFF/API-proxy is needed for V1 (assumed not; direct SPA→API with CORS scoped to front-end origins).
- Exact OpenAPI codegen tool and versioning scheme (owned by software-architect, consumed here).
