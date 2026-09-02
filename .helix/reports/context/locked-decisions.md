# Locked decisions

Reproduced **verbatim** from `inputs/engineering-brief.md` §1 ("Locked vs council-owned"). This is the exhaustive set of locked platform rules for Contigo V1. Do not add extra locked rules beyond this table. See brief §1 for the full "Locked vs council-owned" section.

| Decision | Guideline |
| --- | --- |
| Cloud | Microsoft Azure. |
| Environments | Two from day one: **`dev`** and **`demo`**. No production yet. Isolated from each other (data, identities, resource groups). |
| Cost | Use free tiers and the cheapest SKUs that still satisfy the product spec. Nothing idle-expensive. No production HA / multi-region for now. |
| IaC | HCP Terraform. Infra code lives in the `infra/` folder of the monorepo. |
| Backend | C# / ASP.NET Core (current LTS at implementation time). Modular monolith + background worker. No microservices split in V1. |
| Frontend / mobile | Council decides the stacks. |
| Source control | GitHub account **lucalamalfa91**. **One public** repository [`contigo`](https://github.com/lucalamalfa91/contigo) (see §2). Description "Contigo platform". Not four remotes. |
| Delivery | GitHub CI/CD releases to Azure `dev` and Azure `demo`. |
| AI | Microsoft Foundry only, via a Contigo **AI Gateway**. Domain modules never call a provider directly. Use the cheapest Foundry models that still meet the product tasks. |
| Auth / secrets | OIDC, SSO-ready (Entra ID). Secrets in Key Vault. No secrets in code, client bundles, or Terraform source. |
| API | API-first. Web and mobile consume the backend API. |
| Code authoring | Claude Code via Helix, for infra, backend, web, and mobile. |

Pointer: `inputs/engineering-brief.md` §1 — "Locked vs council-owned". Items in that brief's "Council decides" list are NOT locked and are owned by the Helix council; see `council-open-questions.md`.
