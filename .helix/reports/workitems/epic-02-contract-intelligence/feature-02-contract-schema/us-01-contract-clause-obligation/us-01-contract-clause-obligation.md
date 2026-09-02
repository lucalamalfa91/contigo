---
id: us-01
type: user-story
parent: feature-02
wave: R1
status: active
---

# us-01-contract-clause-obligation — Contract + clause + obligation entities

## Story

As a **backend engineer**, I want the Contract, ContractLineItem, ContractClause,
Obligation, Risk, and CorrectionHistory entities with `tenant_id`, evidence, and
confidence columns, so that extracted facts have a normalized, RLS-guarded canonical
home.

## Acceptance criteria

- [ ] AC-1 All spec §6 entities exist with min V1 fields + `tenant_id`.
- [ ] AC-2 Evidence/source span + confidence columns are on every consequential fact.
- [ ] AC-3 EF Core migrations are code-first and apply cleanly (ADR-003/009).

## Definition of done

- [ ] `dotnet ef migrations add` + `database update` succeed; schema test enumerates entities.
- [ ] honours ADR-003, ADR-009.

## Dependencies

| Depends on | Why |
|------------|-----|
| E01/F04/US02 (relational-store) | DbContext + pgvector already wired |

## Architecture decisions in force

- ADR-003 (PostgreSQL + pgvector), ADR-009 (tenant_id).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Contract/clause/obligation/risk entities + migrations | L | phase-12 |
| task-02 | Evidence/confidence/version columns + schema test | M | phase-13 |

## Council decisions carried into this story

Normalized contract hierarchy (Supplier → Contract Family → MSA/Order Form/Amendment/SOW/Renewal), versioned history, no destructive overwrite.

## Open questions

- none.
