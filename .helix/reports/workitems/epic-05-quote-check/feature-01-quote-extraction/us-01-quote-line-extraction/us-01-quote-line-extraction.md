---
id: us-01
type: user-story
parent: feature-01
wave: R4
status: active
---

# us-01-quote-line-extraction — Quote upload + line-item extraction

## Story

As a **Procurement** user, I want to upload a supplier quote and get line items
extracted, so that I can assess the proposal before signature.

## Acceptance criteria

- [ ] AC-1 `POST /api/quotes` uploads a quote and creates an extraction job.
- [ ] AC-2 Line items extract quantity/SKU/edition/price/discount/term (evidence + confidence).
- [ ] AC-3 Separate arithmetic from LLM language (App C #6).
- [ ] AC-4 Scanned/image quote PDFs reuse the epic-02 hybrid OCR path (ADR-017); no 2-page cap.

## Definition of done

- [ ] `dotnet test` proves quote → line items with evidence + confidence.
- [ ] honours ADR-002, ADR-004, ADR-003.

## Dependencies

| Depends on | Why |
|------------|-----|
| epic-02 (extraction pipeline) | reuses staged extraction |

## Architecture decisions in force

- ADR-002 (Quotes), ADR-004 (extract + ocr), ADR-003, ADR-017, App C #6.

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Quote upload + line-item extraction | L | phase-30 |
| task-02 | Line-item normalization + evidence/confidence | M | phase-31 |

## Council decisions carried into this story

Line-level extraction with evidence; deterministic money math in code.

## Open questions

- none.
