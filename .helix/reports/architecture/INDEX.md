# Contigo V1 — Architecture Decision Records (INDEX)

Accepted ADRs promoted at council-close (2026-09-01/02), plus ADR-017 (OCR in V1). Source drafts live under
`reports/architecture/draft/<seat>/`; accepted copies below carry the canonical `ADR-NNN` number and
`Status: accepted`. Supporting (non-ADR) lane artefacts are listed separately and remain under their
seat's draft folder.

| ADR | Topic | Seat | One-line decision |
| --- | --- | --- | --- |
| ADR-001 | V1 scope R0–R4 | product-owner | User-visible wave ladder; §1.2 non-goals out of scope; R3/R4 on fixture adapter, never a paid API for first `demo`. |
| ADR-002 | .NET solution shape | software-architect | Modular monolith: one project per bounded context + shared kernel + thin API/worker hosts. |
| ADR-003 | Relational store | software-architect | PostgreSQL Flexible Server + pgvector via EF Core/npgsql; RLS tenancy; single system of record. |
| ADR-004 | Foundry model roles | software-architect | Role-split (ocr/classify/extract/embed/answer) behind AI Gateway, config-selected cheapest IDs. |
| ADR-005 | Azure services + SKUs | cloud-architect | Container Apps (consumption) + Postgres Burstable + Storage + Service Bus Standard + Key Vault + Entra ID Free. |
| ADR-006 | Region | cloud-architect | West Europe (`westeurope`) for both `dev` and `demo`. |
| ADR-007 | Terraform layout | cloud-architect | Reusable modules + two env roots; remote state per env; no secrets in source. |
| ADR-008 | Foundry account shape | cloud-architect | One hub, two projects (`dev`/`demo`), one pay-as-you-go AI services account. |
| ADR-009 | Tenancy / RLS | security-architect | Postgres RLS on every tenant table; app passes `tenant_id`; RLS is the non-bypassable backstop. |
| ADR-010 | Entra ID / OIDC | security-architect | Per-env pair (public client + API registration), Authorization Code + PKCE, four registrations total. |
| ADR-011 | Key Vault + RAG isolation | security-architect | Per-env Key Vault + managed identity + OIDC federation; authz-before-retrieval; no-training; input-hash logging. |
| ADR-012 | Web stack | client-architect | React + TypeScript + Vite SPA, OIDC PKCE, static bundle on Static Web Apps free tier. |
| ADR-013 | Mobile stack | client-architect | React Native (Expo) + TypeScript, non-gating lane, no store release for R0–R4. |
| ADR-014 | Git flow | delivery-manager | Trunk-based, protected `main`, PR required; `main`→`dev` auto-deploy; tag + env approval for `demo`. |
| ADR-015 | CI → Azure auth | delivery-manager | OIDC federated credentials, per-env least-privilege service principals; no stored secrets. |
| ADR-016 | Promotion dev→demo | delivery-manager | Tag + `demo` GitHub Environment with required reviewers; code/artifacts only, never data. |
| ADR-017 | OCR in V1 | software-architect | Hybrid native-text + Azure AI Document Intelligence (`prebuilt-read`/`prebuilt-layout`) behind the AI Gateway; full document; not deferred. |

## Supporting artefacts (not ADRs)

| File (draft) | Seat | Purpose |
| --- | --- | --- |
| `draft/product-owner/scope-notes.md` | product-owner | Personas, non-goals, day-1 promise, acceptance hooks (seed for user stories). |
| `draft/software-architect/module-map.md` | software-architect | Module boundaries, entity ownership, dependency direction, worker responsibilities. |
| `draft/client-architect/api-consumption.md` | client-architect | One versioned OpenAPI contract → one generated TS client; OIDC; config-not-code. |
| `draft/delivery-manager/wave-calendar.md` | delivery-manager | Wave order + calendar (S0 → R0–R4, ~18 weeks) and environment plan. |

## Required-ADR coverage check

All fourteen required topics from the council-protocol brief are covered by ADR-001 through ADR-016
(scope/R0–R4, git flow, Azure SKUs, region, Terraform layout, .NET solution, web, mobile, Foundry
models, CI→Azure auth, promotion, relational store, tenancy/RLS, Key Vault+RAG). ADR-017 closes the
CQ-008 sub-item "OCR vs native document parse": OCR is in V1, not deferred. No required topic
remains unaddressed.
