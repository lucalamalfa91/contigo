# Contigo

Single public GitHub monorepo for the Contigo platform.

- **Repository**: [`lucalamalfa91/contigo`](https://github.com/lucalamalfa91/contigo)
- **Owner**: `lucalamalfa91` — a personal GitHub **user** account. Contigo is
  not a GitHub organization (ADR-014).
- **Visibility**: public
- **Description**: Contigo platform
- **Default branch**: `main` — trunk-based, protected (ADR-014)

## Folder layout

One monorepo, four product domains plus the Helix run artefact — not four
separate remotes (ADR-014):

| Folder | Contents |
|--------|----------|
| `infra/` | Terraform / infrastructure-as-code for the Azure `dev` and `demo` environments |
| `backend/` | .NET modular-monolith API + workers |
| `web/` | React + TypeScript SPA |
| `mobile/` | React Native (Expo) app |
| `.helix/` | Helix run artefact — context, ADRs, work items, plan, this delivery process |

## Branching and protection

Trunk-based: every change lands on `main` through a required pull request.
`main` is protected —

- pull request required to merge (no direct push, including for admins),
- status checks required to pass,
- no force-pushes, no branch deletion.

Merges to `main` auto-deploy to the `dev` environment; promotion to `demo`
is a separate tagged, environment-approved step (ADR-016). See
`.helix/reports/architecture/ADR-014-git-flow.md` for the full decision
record.

## Verifying the repo shape and protection

These two scripts make the repo identity and branch protection reproducible
instead of a one-off console click. Both require the GitHub CLI (`gh`)
authenticated against `lucalamalfa91/contigo`.

```bash
# owner/repo/visibility/description/default-branch + folder layout + secret scan
python scripts/verify_github_repos.py

# applies (idempotently) and confirms `main` branch protection
python scripts/apply_github_branch_protection.py
```

Both exit `0` when the repository already matches — or has just been
brought to match — the required shape, and non-zero with a report of what
does not.
