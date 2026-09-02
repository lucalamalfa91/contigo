# Open questions

Decisions this run needed and did not have. Every entry carries an **assumption
in force**, so the pipeline can keep moving. This file is not a halt.

Status: `open` · `answered` · `assumed-confirmed` · `assumed-wrong`

---

## How to use this file

**Agents**: never invent a locked decision. Add an entry, state the assumption,
build against it, and reference it from the ADR or task. An assumption you
record is a result; an assumption you absorb silently is a defect.

Mark a wave-spec task `status: gated` only when **no** assumption is defensible.

---

## Client-architect lane (independent)

- **OQ-client-001** — Azure Static Web Apps free tier is available and sufficient in the chosen region. **Status**: `assumed-confirmed`. **Assumption in force**: SWA free tier (TLS + CDN) hosts the static web bundle. Confirm SKU/region with cloud-architect at council-close. Ref: ADR-web-stack.
- **OQ-client-002** — Entra ID (OIDC) supports the Authorization Code + PKCE public-client flow for `dev`/`demo` tenants. **Status**: `assumed-confirmed`. **Assumption in force**: clients are public clients; no client secret in bundle. Confirm with security-architect. Ref: ADR-web-stack, ADR-mobile-stack, api-consumption.
- **OQ-client-003** — No BFF/API-proxy is required for V1; the SPA calls the API origin directly with CORS scoped to front-end origins. **Status**: `assumed-confirmed`. Ref: api-consumption.
- **OQ-client-004** — A mobile store release is genuinely out of scope for V1 `dev`/`demo` (no store-release dependency in spec §16/§20). **Status**: `assumed-confirmed`. Ref: ADR-mobile-stack.
- **OQ-client-005** — Mobile CI lanes can be configured as non-blocking in the council git flow/CI. **Status**: `assumed-confirmed`. Confirm with delivery-manager. Ref: ADR-mobile-stack.
- **OQ-client-006** — Expo/React Native supports OIDC Authorization Code + PKCE against Entra ID for public clients. **Status**: `assumed-confirmed`. Ref: ADR-mobile-stack.
- **OQ-client-007** — OpenAPI codegen tool and URL versioning scheme (e.g. `/v1/...`) are owned by software-architect; clients consume one generated TypeScript client. **Status**: `open` — resolve with software-architect at council-close. Ref: api-consumption.

## Delivery-manager lane (independent)

- **OQ-DM-001** — GitHub Environments with required reviewers are available on the `lucalamalfa91/contigo` public-repo plan; if not, `demo` promotion falls back to a protected tag + a PR to a `demo/*` pointer (still explicit/manual). **Status**: `assumed-confirmed`. Ref: ADR-git-flow, ADR-promotion-dev-demo.
- **OQ-DM-002** — The reviewers for the `demo` environment approval gate are product-owner + security-architect during V1; authority is council-owned. **Status**: `assumed-confirmed`. Ref: ADR-git-flow, ADR-promotion-dev-demo.
- **OQ-DM-003** — GitHub OIDC federation to the customer's Entra ID tenant is permitted on the org plan; fallback is a short-lived SP certificate in Key Vault (never a plaintext GitHub secret). **Status**: `assumed-confirmed`. Ref: ADR-ci-azure-auth.
- **OQ-DM-004** — OIDC subject-claim pinning for `demo` (environment + tag) is sufficient and the org does not allow unrestricted fork-triggered OIDC; deploy jobs run with `pull_request: false`. **Status**: `assumed-confirmed`. Ref: ADR-ci-azure-auth.
- **OQ-DM-005** — Tag naming `demo-v*` and environment name `demo` are council-owned and may be renamed; only the *mechanism* (tag + gated environment) is fixed. **Status**: `assumed-confirmed`. Ref: ADR-promotion-dev-demo.
- **OQ-DM-006** — No absolute start date exists in the brief; the wave calendar uses sequential weeks from an unspecified kickoff, not named calendar dates. **Status**: `assumed-confirmed`. Ref: wave-calendar.
- **OQ-DM-007** — Wave durations (~18 weeks R0–R4 + 2 weeks S0) are first-pass planning estimates owned downstream by the decomposer; this lane commits no hours/dates. **Status**: `assumed-confirmed`. Ref: wave-calendar.

## Security-architect lane (independent)

- **OQ-sec-001** — The chosen relational store (PostgreSQL + pgvector) supports `FORCE ROW LEVEL SECURITY` on the cheapest managed SKU that meets product constraints. **Status**: `assumed-confirmed`. **Assumption in force**: RLS is available; if the software-architect's SKU choice drops RLS, tenancy must force a SKU that keeps it (RLS is non-optional). Ref: ADR-tenancy.
- **OQ-sec-002** — Microsoft Foundry in the chosen region offers a no-training model endpoint (or an opt-out) for the contract-content models. **Status**: `assumed-confirmed`. **Assumption in force**: the AI Gateway selects a no-training endpoint; final model IDs confirmed jointly with software-architect + cloud-architect in the Foundry model-ID ADR. Ref: ADR-secrets-and-rag.

## Software-architect / council (OCR)

- **OQ-ocr-001** — OCR vs native document parse (brief §8, former CQ-008 sub-item). **Status**: `answered`. OCR is in V1: hybrid native-text + Azure AI Document Intelligence (`prebuilt-read` / `prebuilt-layout`) behind the AI Gateway; full document; no 2-page cap. Ref: ADR-017, ADR-004, ADR-005, ADR-008.

## Implementer / E01/F04/US02/T01 (EF Core + pgvector wiring)

- **OQ-impl-001** — ADR-004 fixes the embed-role model as "Foundry embedding model (e.g.
  `text-embedding-3-small` or `text-embedding-3-large`)... dimension fixed at schema time; small
  dimension preferred" but does not pin an exact integer. **Status**: `assumed-confirmed`.
  **Assumption in force**: `Embedding.Vector` is a fixed `vector(1536)` Postgres column
  (`Embedding.VectorDimensions` constant), matching `text-embedding-3-small`'s native output size
  — the smaller of the two named candidates, per ADR-004's "small dimension preferred for
  cost/size." If the council/AI Gateway later selects a different embed model with a different
  native dimension, this column width is a migration, not a redesign. Ref: ADR-003, ADR-004,
  `backend/src/Contigo.Documents.Contracts/Domain/Embedding.cs`.
- **OQ-impl-002** — ADR-003/ADR-009 write every table/column name in lowercase snake_case
  (`tenant_id`, `document`, `contract`, `embedding`, `clause`, ...), but neither ADR names an EF
  Core naming convention mechanism. **Status**: `assumed-confirmed`. **Assumption in force**: the
  Documents/Contracts `DbContext` calls `UseSnakeCaseNamingConvention()` (`EFCore.NamingConventions`
  package) so the physical schema matches the ADRs' own naming verbatim — without it, Npgsql/EF
  Core would emit quoted PascalCase identifiers instead. This convention is now load-bearing for
  every future migration in this module; us-03's RLS policies (`CREATE POLICY ... USING
  (tenant_id = ...)`) can rely on the lowercase column names existing as written. Ref: ADR-003,
  ADR-009, `backend/src/Contigo.Documents.Contracts/Infrastructure/DocumentsContractsDbContextOptions.cs`.
