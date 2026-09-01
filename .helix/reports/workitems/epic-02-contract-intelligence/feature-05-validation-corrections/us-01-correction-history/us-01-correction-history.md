---
id: us-01
type: user-story
parent: feature-05
wave: R1
status: active
---

# us-01-correction-history — Correction history + versioning

## Story

As a **Procurement/Legal** user, I want to correct a low-confidence extraction so that
my correction is versioned (not overwriting the original) and the audit trail is
preserved.

## Acceptance criteria

- [ ] AC-1 `PATCH /api/contracts/{id}` records a correction as a new version.
- [ ] AC-2 Original AI extraction is preserved; correction history is queryable.
- [ ] AC-3 Deterministic fields are versioned, never destructively overwritten (App C #5/#9).

## Definition of done

- [ ] `dotnet test` proves original extraction survives a correction and history is retrievable.
- [ ] honours ADR-003 (CorrectionHistory), ADR-009.

## Dependencies

| Depends on | Why |
|------------|-----|
| feature-02 (schema) | correction writes ContractVersion |

## Architecture decisions in force

- ADR-003 (version entities), ADR-009 (RLS), App C #5/#9.

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Correction history entity + versioned PATCH | M | phase-17 |
| task-02 | Audit event on correction + history query | S | phase-18 |

## Council decisions carried into this story

Versioned history; corrections are new versions, never destructive.

## Open questions

- none.
