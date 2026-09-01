# ADR-003 — Relational store: PostgreSQL + pgvector with EF Core + npgsql

- **Status**: accepted
- **Date**: 2026-09-02
- **Deciders**: software-architect (owner); cloud-architect (SKU/host), security-architect (RLS tenancy) reconcile at council-close
- **Locked citations**: Cloud — Azure; Cost — cheapest SKU that satisfies the spec; Backend — C#/ASP.NET Core LTS (locked-decisions.md). Database guideline — SQLite acceptable on dev laptop only; on Azure use cheapest managed relational store satisfying tenant isolation, shared API+worker, embeddings/search, durable shared storage; non-relational store must not be the system of record (brief §5).

## Context and problem statement

The brief forbids SQLite on Azure and requires the **cheapest managed relational store** that satisfies: tenant isolation at the DB level, a shared API + worker writing to the same store, embeddings/semantic search, and durable shared storage (brief §5). The spec's deployable topology explicitly names **PostgreSQL + pgvector** (spec §5.1) and requires P0 "PostgreSQL domain schema + migrations" (spec priority list).

The question is the concrete engine, access library, and how vectors/search and tenancy are satisfied without exceeding cost or coupling modules.

## Decision drivers

- PostgreSQL + pgvector is named in the spec topology and requires no second search engine in V1.
- Must satisfy tenancy isolation at DB level and RAG auth-before-retrieval (brief §10, Appendix C rule 4).
- C# / .NET ecosystem: EF Core is the natural ORM; npgsql is the ADO.NET/EF Core provider for PostgreSQL.
- One system of record; no non-relational store as the source of truth.

## Considered options

1. **Azure Database for PostgreSQL Flexible Server (pgvector) + EF Core + npgsql** — single managed Postgres with the `vector` extension; EF Core migrations; RLS for tenancy.
2. **Citus / Hyperscale (Citus)** — distributed Postgres for scale.
3. **Cosmos DB (NoSQL) as system of record + separate Postgres** — non-relational primary.

## Decision outcome

**Chosen: Option 1** — Azure Database for PostgreSQL Flexible Server with the `pgvector` extension, accessed through EF Core (npgsql provider) with a code-first migration pipeline and Postgres Row-Level Security for tenant isolation. This is because it is the spec-named engine, satisfies embeddings/search and tenancy in one managed store without a second system of record, and keeps ownership costs at the cheapest tier that the storage/vector features allow (exact SKU owned by cloud-architect).

### Consequences

- **Good**: One system of record; vectors live next to relational facts (no sync between two stores); RLS enforces tenant isolation at the DB level; EF Core migrations give a repeatable P0 schema pipeline; npgsql supports `pgvector`.
- **Bad**: Flexible Server is not serverless/free — it has a base cost that must be minimized (burstable/compute size) and may not scale-to-zero, so it must be sized/stoppable by cloud-architect under the cost guideline. RLS adds a design obligation on every table (a policy per table) that the decomposer must encode.
- **Neutral**: `pgvector` is an extension, not a separate service; index choice (HNSW vs IVFFlat) is a later tuning decision, not an architecture fork.

## Pros and cons of the options

### Option 1 — Azure Database for PostgreSQL Flexible Server + pgvector + EF Core/npgsql
- Good: spec-named; single store; vectors + relational + RLS in one place; mature .NET access path.
- Bad: non-zero base cost; RLS discipline required; `pgvector` feature availability must be confirmed for the chosen server type/region.

### Option 2 — Citus/Hyperscale
- Good: horizontal scale path.
- Bad: overkill for two non-production environments; higher cost; against "no production HA/multi-region" framing; not needed for V1 volume.

### Option 3 — Cosmos DB primary + Postgres secondary
- Good: flexible schema.
- Bad: violates "non-relational store must not be the system of record"; introduces a second store and sync; unnecessary complexity.

## Implications for the decomposition

Every table carrying business data MUST have a `tenant_id` column and a Postgres RLS policy that restricts rows to the current tenant (security-architect owns the exact RLS mechanics). Database changes MUST be expressed as EF Core migrations (no hand-edited DDL drift). The `pgvector` extension MUST be enabled in the Terraform/managed-server provisioning so the `vector` column type and similarity search are available. Deterministic calculations stay in domain code and only persist *results*, never store derived truth that an LLM computed. Embeddings are stored in the Documents/Contracts context and query-time RAG MUST apply RLS so unauthorized documents are never retrieved (Appendix C rule 4).

## Assumptions

- Exact SKU/tier (burstable compute size, storage, backup retention) is owned by cloud-architect; the store is a Flexible Server, not Single Server (Single Server is being retired).
- `pgvector` is available on the chosen Postgres version/region (confirmed at implementation time).
- EF Core Core version aligns with the .NET LTS chosen in ADR-dotnet-solution.
