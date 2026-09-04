# Product README hygiene

Every product domain has a README at the folder root. Those files are the
operator-facing contract for how to run, test, plan, and deploy that
folder. They go stale the moment a task changes public surface and does
not touch them.

This skill is standing scope for passata 2. It does **not** belong in
`## Files to create or modify` on every task (that would make every
same-phase task a single-writer collision on one markdown file). Update
the README in the **same commit** as the code.

## Which file

| Diff under | Update |
|---|---|
| `infra/` | `infra/README.md` |
| `backend/` | `backend/README.md` |
| `web/` | `web/README.md` |
| `mobile/` | `mobile/README.md` |
| `.github/workflows/`, identities, env names, promotion, or a new top-level folder | root `README.md` **and** the domain README the workflow deploys |

A missing domain README, when that domain is in the diff, is a defect —
create it, do not wait for a dedicated docs task.

## What counts as public surface (must update)

- Commands (`dotnet`, `npm`, `terraform`, scripts) added, renamed, or removed
- Folder / project / module layout
- HTTP routes, queue contracts, OpenAPI, runtime config keys, env vars
- Terraform modules, resource names, SKUs, region, backend/workspace
- CI/CD path, GitHub Environment, who applies (HCP vs GHA vs CLI)
- Identities and which process uses which app
- A gap that an operator will hit (placeholder image, missing grant, interim header)

## What does not (leave the README alone)

- Tests-only, comments-only, or an internal rename with no operator-visible effect
- A follow-up that only fills in a gap the README already documents honestly

Rewrite the stale sentence. Do not append a changelog. Do not copy ADR
prose. Name the ADR id when the README states a locked decision.

## Reviewer

Blocking when the diff changes public surface and the matching README is
unchanged, missing, or still describes the old command/route/module/gap.
`SUGGESTION` only when the change is genuinely invisible outside the
folder.

## Conflict-fixer

Parallel tasks will collide on these files. Union both sides' factual
updates (new command + new route both stay). Do not drop a newly
documented gap or identity warning from either side.

## Staging

Root `README.md` is **not** covered by `git add -- infra backend web
mobile`. Stage it explicitly:

```bash
git add -- README.md infra backend web mobile workspace .github scripts
```
