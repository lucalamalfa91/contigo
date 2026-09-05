---
id: us-01
type: user-story
parent: feature-05
wave: 6
status: active
---

# us-01-document-upload — Upload + processing pipeline

## Story

As a **Procurement user**, I want to upload a contract and watch it process, so
I can ingest documents into the workspace.

## Acceptance criteria

- [ ] AC-1 Dropzone (drag-and-drop + file picker), formats/size/sources strip.
- [ ] AC-2 6-stage processing pipeline (current pulsing).
- [ ] AC-3 Result card per outcome (needs_review / completed / failed).

## Definition of done

- [ ] Upload → processing visible in browser on `demo`.
- [ ] honours ADR-020 (screen 3), ADR-019 (states).

## Dependencies

| Depends on | Why |
|------------|-----|
| feature-03 us-02 (shell) | nav + wiring |

## Architecture decisions in force

- ADR-020 (screen 3), ADR-019 (loading/error/empty states).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Upload + processing pipeline UI | M | phase-06 |

## Council decisions carried into this story

Empty/error/loading are part of the Day-1 path, not garnish.
