---
id: us-01
type: user-story
parent: feature-09
wave: R0
status: active
---

# us-01-final-integration — R0 Platform integration

## Story

As a **product owner**, I want the R0 definition of success proven end-to-end, so
that "a secure workspace can ingest documents" on `dev`/`demo`.

## Acceptance criteria

- [ ] AC-1 Authenticate → create workspace → invite → upload document → audit event.
- [ ] AC-2 Cross-tenant isolation holds across the whole path.
- [ ] AC-3 Same path repeatable on `demo` (tag + environment approval).

## Definition of done

- [ ] `dotnet test` (integration) runs the R0 path; `demo` smoke documented.

## Dependencies

| Depends on | Why |
|------------|-----|
| every R0 leaf artifact | proves the wave |

## Architecture decisions in force

- ADR-001, ADR-002, ADR-003, ADR-009, ADR-010, ADR-011, ADR-016.

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | R0 end-to-end integration | L | phase-26 |

## Council decisions carried into this story

R0 success = secure workspace ingests documents.

## Open questions

- none.
