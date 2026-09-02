# Engineering constraints (Helix input)

Canonical mandate: `docs/contigo_v1_engineering_brief.md`  
Product (what to build): `docs/contigo_v1_technical_product_specification.md`

- Read the product spec only for scope.
- Treat the engineering brief as **guidelines that are locked**. Do not add extra locked rules.
- Everything the brief lists under “Council decides” is owned by the process — do not adopt an implied default (git flow, SKUs, models, libraries, region).
- All code is written by Claude Code through Helix.
- Azure: `dev` + `demo`, isolated. GitHub CI/CD releases to both. Git flow is a council ADR.
- Source control (locked, v1.2): GitHub account **lucalamalfa91**, **one public** repository [`contigo`](https://github.com/lucalamalfa91/contigo) (monorepo, description "Contigo platform"). Domain folders `infra/`, `backend/`, `web/`, `mobile/` plus `.helix/` (process; not a nested git). Not four remotes. Not `workspace/<repo>/` as a stand-in.
- Helix passata 2 worktrees that one repository. Claude Code cwd is the worktree root.
- Do not put SQLite on Azure. Do not share data stores between `dev` and `demo`.
