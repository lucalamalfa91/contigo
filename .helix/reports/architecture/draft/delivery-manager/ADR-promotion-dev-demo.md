# ADR — Promotion of a release from dev to demo

- **Status**: proposed
- **Date**: 2026-09-01
- **Deciders**: delivery-manager (+ reconciliation with cloud-architect & security-architect at close)
- **Locked citations**:
  - Environments — "Two from day one: `dev` and `demo` … Isolated from each other (data, identities, resource groups)."
  - Delivery — "GitHub CI/CD releases to Azure `dev` and Azure `demo`."
  - Brief §2.1 — "Promotion to `demo` is explicit, not an accidental copy of every `dev` deploy."
  - Brief §4 — "`dev` and `demo` must not share PostgreSQL (or equivalent) or document storage."

## Context and problem statement

`dev` is the integration environment; `demo` is the stakeholder-facing environment. Both are deployed
from the same monorepo by the same CI, but `demo` must not receive every `dev` change automatically —
promotion is a deliberate, reviewable act, and the two environments must never share a database or
document storage (brief §2.1, §4). The flow in `ADR-git-flow.md` establishes trunk-based `main` that
auto-deploys to `dev`; this ADR defines the *explicit* promotion step to `demo`.

## Decision drivers

- **Explicitness** — an uncontrolled copy of every `dev` deploy is explicitly disallowed.
- **Isolation** — promotion must not couple `dev` and `demo` data stores; it only moves code/artifacts,
  not data.
- **Human approval** — `demo` is stakeholder-facing, so a human gate (required reviewer) must be in the
  path.
- **Reproducibility for Claude Code** — the promotion must be a named, single-command step (a tag), not
  a manual re-plumb.

## Considered options

1. **Tag-triggered promotion + `demo` GitHub Environment approval** — a tag on `main` (e.g. `demo-v*`)
   plus a `demo` environment with required reviewers triggers the `demo` deploy jobs.
2. **Manual workflow_dispatch with environment selector** — a human clicks "Run" and chooses `demo`; no
   tag.
3. **Separate `demo` long-lived branch** — a `demo` branch that only receives cherry-picks/merges when
   promotion is wanted.

## Decision outcome

**Chosen: Option 1 — tag-triggered promotion gated by a `demo` GitHub Environment with required
reviewers.**

A promotion is the single act of tagging a `main` commit (`demo-v<seq>`) and approving the resulting
`demo` deploy in the GitHub Environment. No tag, no `demo` deploy. This is explicit, auditable, and maps
to one instruction Claude Code (or a human) executes. The `demo` environment runs against the `demo`
service principal and `demo` resource group only (see `ADR-ci-azure-auth.md`), so data isolation is
structural, not just convention.

### Consequences

- **Good**: promotion is a named, reviewable event; `demo` never drifts onto arbitrary `dev` commits;
  the tag is an immutable rollback point (re-tag a known-good SHA).
- **Bad**: requires discipline to tag and approve; `demo` lags `dev` by however long the approval takes
  (intended — `demo` is curated, not continuous).
- **Neutral**: tags exist only as promotion markers, not as a second long-lived branch.

## Pros and cons of the options

### Option 1 — Tag + environment approval (chosen)
- Good: explicit, auditable, isolated, cheap, minimal ceremony.
- Bad: an extra approval step; humans must review before `demo` is refreshed.

### Option 2 — Manual dispatch
- Good: simplest mechanically.
- Bad: no immutable reference to *what* was promoted (no tag/SHA link is guaranteed), weaker audit, and
  easier to promote a wrong SHA by mistake.

### Option 3 — Long-lived `demo` branch
- Good: an explicit line.
- Bad: re-introduces a second branch, cherry-pick drift, and conflict surface that trunk-based flow
  deliberately avoids; a branch is a *version* not a *when-to-promote* statement.

## Implications for the decomposition

- `dev` deploy: on merge to `main`, gated by path filters; runs under the `dev` CI identity.
- `demo` deploy: on tag `demo-v*` on `main`, gated by a `demo` GitHub Environment with required
  reviewers (human approval); runs under the `demo` CI identity and `demo` resource group.
- Promotion moves **code/artifacts only**; never copies a database, storage account, or secrets between
  environments — each environment's data plane is created/owned in its own Terraform state.
- Rollback for `demo` = re-tag a known-good `main` SHA (the environment emits which tagged release is
  live).

## Assumptions

- (open-question OQ-DM-005) Tag naming `demo-v*` and environment name `demo` are council-owned and may
  be renamed at close; the requirement is only that the *mechanism* (tag + gated environment) stays.
- (open-question OQ-DM-002) The required reviewers for the `demo` environment are product-owner +
  security-architect during V1 — assumption until the council ratifies ownership.
- No data-plane replication between `dev` and `demo` is ever performed by CI; this is a hard constraint
  of the locked isolation rule, not a policy this ADR may relax.
