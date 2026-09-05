---
id: us-02
type: user-story
parent: feature-05
wave: 6
status: active
---

# us-02-document-status-readback — Document status read-back

## Story

As a **Procurement user**, I want a document table showing processing status, so
I can see which documents are done vs need review vs failed.

## Acceptance criteria

- [ ] AC-1 Document table: Document/Type/Supplier/Status/Uploaded.
- [ ] AC-2 Status tags honour ADR-019 semantic mapping (completed/needs_review/failed).
- [ ] AC-3 Row opens Contract 360 (cross-link).

## Definition of done

- [ ] Status read-back renders in browser on `demo`.
- [ ] honours ADR-020 (screen 3), ADR-019.

## Dependencies

| Depends on | Why |
|------------|-----|
| us-01 (upload) | document source + status |

## Architecture decisions in force

- ADR-020 (screen 3), ADR-019 (status tags).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Document status read-back UI | S | phase-07 |

## Council decisions carried into this story

needs_review/failed are first-class statuses (spec §7.1).
