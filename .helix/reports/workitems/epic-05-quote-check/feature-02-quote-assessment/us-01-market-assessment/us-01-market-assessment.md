---
id: us-01
type: user-story
parent: feature-02
wave: R4
status: active
---

# us-01-market-assessment — Benchmark matching + market assessment

## Story

As a **Procurement** user, I want a line-level market assessment (above/in-line/
below + target range + potential saving), so that I know the quote's market position.

## Acceptance criteria

- [ ] AC-1 Match normalized line items to the Benchmark Service (multi-dimensional).
- [ ] AC-2 Flag above/in-line/below market + recommended target range + potential saving.
- [ ] AC-3 `GET /api/quotes/{id}/assessment` returns the assessment with confidence/provenance.

## Definition of done

- [ ] `dotnet test` proves assessment + target/saving from fixture benchmark.
- [ ] honours ADR-002, ADR-001 (fixture), spec §11.2.

## Dependencies

| Depends on | Why |
|------------|-----|
| feature-01 (quote extraction) | line items feed the match |
| epic-04 (benchmark service) | comparison needs benchmark |

## Architecture decisions in force

- ADR-002 (Quotes → Benchmark), ADR-001 (fixture), spec §11.2.

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Benchmark matching + market assessment | L | phase-32 |
| task-02 | Target range + saving computation | M | phase-33 |

## Council decisions carried into this story

Market assessment per spec §11.2; deterministic target/saving.

## Open questions

- none.
