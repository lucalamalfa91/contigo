---
id: us-01
type: user-story
parent: feature-06
wave: R0
status: active
---

# us-01-document-upload — Document upload + metadata

## Story

As a **Workspace Admin/Procurement user**, I want to upload a document into
tenant-scoped storage with metadata/status, so that ingestion can be tracked.

## Acceptance criteria

- [ ] AC-1 `POST /api/documents` stores bytes in tenant-scoped blob (no cross-tenant path).
- [ ] AC-2 Document metadata + processing status is persisted.
- [ ] AC-3 `GET /api/documents/{id}` returns metadata/status for the caller's tenant.

## Definition of done

- [ ] `dotnet test` proves tenant-scoped storage + metadata round-trip.
- [ ] honours ADR-009, ADR-011, ADR-003.

## Dependencies

| Depends on | Why |
|------------|-----|
| feature-05 (workspace) | upload within a workspace/tenant |

## Architecture decisions in force

- ADR-009 (RLS), ADR-011 (storage isolation), ADR-003 (PostgreSQL).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Upload → tenant-scoped blob + job | M | phase-19 |
| task-02 | Metadata + status + GET | S | phase-20 |

## Council decisions carried into this story

Tenant-scoped storage paths; no cross-tenant data plane.

## Open questions

- none.
