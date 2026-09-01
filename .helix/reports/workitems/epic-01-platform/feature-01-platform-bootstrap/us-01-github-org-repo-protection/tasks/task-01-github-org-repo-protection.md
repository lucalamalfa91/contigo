---
id: E01/F01/US01/T01
type: task
story: us-01-github-org-repo-protection
wave: R0
status: live
target_repo: contigo
# requires: [github_admin]
---

# task-01-github-org-repo-protection — Adopt `lucalamalfa91/contigo` + protect `main`

## Coding objective

Do **not** create a GitHub organization. The product remote already exists:
[`https://github.com/lucalamalfa91/contigo`](https://github.com/lucalamalfa91/contigo)
(owner `lucalamalfa91`, name `contigo`, description **Contigo platform**,
**public**). That repository is the Helix run repo and the only product remote
(ADR-014: one public monorepo, not four remotes). Confirm it, keep it public,
keep the description, and ensure the
domain folders `infra/`, `backend/`, `web/`, `mobile/`, and `.helix/` exist at
the repo root. On `main` apply branch protection: require pull-request review
before merge, require status checks to pass, and disallow direct pushes and
force-pushes. Use `scripts/verify_github_repos.py` and
`scripts/apply_github_branch_protection.py` (or the GitHub REST API) so the
shape and protection are reproducible, not a manual console gesture. Confirm no
secret material (connection string, key, SAS token, PAT) is committed anywhere
in the repo.

## Parent story AC covered

- AC-1 (`lucalamalfa91/contigo` exists — user account, not a Contigo org)
- AC-2 (single **public** `contigo` monorepo with the five folders + description Contigo platform)
- AC-3 (`main` protected: PR + status checks, no direct push)

## Files to create or modify

| Path | Change |
|------|--------|
| README.md | repo bootstrap doc naming owner/`contigo` + folder layout + the GitHub URL |
| scripts/verify_github_repos.py | assert owner `lucalamalfa91`, repo `contigo`, public, description, default `main` |
| scripts/apply_github_branch_protection.py | assert + set `main` protection on that one repo |
| .gitignore | exclude secrets and local env files |
| infra/, backend/, web/, mobile/, .helix/ | ensure the five domain folders exist (placeholders ok) |

## Context the implementer needs

- **Architecture decisions in force**: ADR-014 (trunk-based, one monorepo, protected `main`).
- **Product remote**: `lucalamalfa91/contigo` — https://github.com/lucalamalfa91/contigo (public)
- **Do not touch**: no application code; no Terraform state yet; do not create a GitHub org; do not make the repo private; do not create `contigo-infra` / `contigo-backend` / `contigo-web` / `contigo-mobile` remotes.

## Definition of done

- [ ] `python scripts/verify_github_repos.py` exits 0 and reports `lucalamalfa91/contigo` (public, description Contigo platform, default branch `main`).
- [ ] `python scripts/apply_github_branch_protection.py` exits 0 and reports `main` is protected (PR + status checks).
- [ ] The repo contains the five folders and no committed secrets (scan exits clean).

## Tests required

| Level | What it proves | Where |
|-------|----------------|-------|
| script | owner/repo/description + `main` protection present | `scripts/verify_github_repos.py`, `scripts/apply_github_branch_protection.py` |

## Open questions blocking this task

- none

## Wave-spec entry

```yaml
- id: E01/F01/US01/T01
  prompt: reports/workitems/epic-01-platform/feature-01-platform-bootstrap/us-01-github-org-repo-protection/tasks/task-01-github-org-repo-protection.md
  produces: [github-org-repos, github-branch-protection]
  depends_on: []
  effort: M
  layer: backend
  status: live
```
