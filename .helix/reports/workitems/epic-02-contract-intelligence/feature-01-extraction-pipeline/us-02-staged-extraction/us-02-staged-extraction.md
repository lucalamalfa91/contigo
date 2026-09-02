---
id: us-02
type: user-story
parent: feature-01
wave: R1
status: active
---

# us-02-staged-extraction — Staged extraction with evidence + confidence

## Story

As a **backend engineer**, I want a staged, schema-constrained extraction job with
native-text + OCR hybrid parsing, so that extracted facts carry source page/section,
confidence, and are persisted as canonical data (not only LLM output).

## Acceptance criteria

- [ ] AC-1 Extraction is staged (metadata → commercial terms → dates → price/SKU → clauses → obligations → risk).
- [ ] AC-2 Every extracted fact carries source span + confidence per spec §7.3.
- [ ] AC-3 Hybrid parse: native text for born-digital; OCR for scanned/image pages (full document, no 2-page cap).

## Definition of done

- [ ] Extraction job on a born-digital fixture **and** a scanned/image fixture persists canonical facts with evidence + confidence (integration test). The scanned path must go through Document Intelligence (ADR-017).
- [ ] honours ADR-004, ADR-002, ADR-017, App C #1/#2/#6.

## Dependencies

| Depends on | Why |
|------------|-----|
| us-01 (this feature) | extraction calls the gateway |
| E01/F06 (document-ingestion) | document bytes + storage path |

## Architecture decisions in force

- ADR-004 (extract + ocr roles), ADR-002 (Documents/Contracts), ADR-011 (isolation), ADR-017 (OCR in V1).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Staged extraction pipeline + evidence/confidence | L | phase-13 |
| task-02 | Hybrid OCR pre-pass behind gateway | M | phase-14 |

## Council decisions carried into this story

OCR in V1 (ADR-017): hybrid native-text + Document Intelligence; full-document; no 2-page cap; page-budget may fail the job, never silently truncate.

## Open questions

- none (spec §7 + ADR-004 + ADR-017 fixed).
