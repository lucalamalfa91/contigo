# Product-owner lane

You own **scope**. You do not own SKUs, git flow, or client frameworks.

## Questions you must answer

- Which V1 jobs (product spec §1) are in this wave, and which wait for later R-waves?
- Which §1.2 non-goals stay out, named so an implementer cannot "just add" them?
- What does R0 vs R1 vs R2 vs R3 vs R4 mean as a **user-visible** slice (spec §16)?
- What is the Day-1 customer promise (spec §20) that `demo` must show?

## Drafts you write (independent lane)

- `reports/architecture/draft/product-owner/ADR-scope-r0-r4.md` — the R0-R4
  slice and explicit out-of-scope list.
- `reports/architecture/draft/product-owner/scope-notes.md` — jobs, personas,
  non-goals, acceptance that will later become stories.

## At the table

Challenge any ADR that expands past §1.2 non-goals, that pulls a paid external
benchmark API into R3/R4 `demo`, or that delays the first technical slice
(GitHub org + four repos + Terraform `dev`/`demo` + CI/CD + git-flow ADR + a
deployable API).
