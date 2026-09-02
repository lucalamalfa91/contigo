---
id: us-02
type: user-story
parent: feature-04
wave: R0
status: active
---

# us-02-relational-store — EF Core + npgsql + pgvector migrations

## Story

As a **backend engineer**, I want the Document/Contract and embedding tables to
persist via EF Core/npgsql with the `pgvector` extension enabled and code-first
migrations, so that Postgres is the single system of record with vectors next to
relational facts.

## Acceptance criteria

- [ ] AC-1 EF Core (npgsql) configured; `vector` column type available (pgvector).
- [ ] AC-2 Code-first migrations are the only schema path (no hand-edited DDL).
- [ ] AC-3 Embeddings live in the Documents/Contracts context; deterministic results are persisted, never LLM-computed truth.

## Definition of done

- [ ] `dotnet ef migrations add` + `database update` succeed against a local Postgres with pgvector.

## Dependencies

| Depends on | Why |
|------------|-----|
| us-01 (feature-04) | projects exist first |

## Architecture decisions in force

- ADR-003 — PostgreSQL Flexible Server + pgvector, EF Core/npgsql, migrations, single store.

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Wire EF Core/npgsql + pgvector + initial migrations | L | phase-4 |

## Council decisions carried into this story

PostgreSQL Flexible Server, `pgvector` extension, EF Core/npgsql, code-first migrations.

## Open questions

- none
