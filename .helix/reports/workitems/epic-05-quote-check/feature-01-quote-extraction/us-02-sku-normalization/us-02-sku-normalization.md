---
id: us-02
type: user-story
parent: feature-01
wave: R4
status: active
---

# us-02-sku-normalization — SKU normalization + correction

## Story

As a **Procurement** user, I want to correct product/SKU matching before accepting
the assessment, so that the comparison is against the right benchmark.

## Acceptance criteria

- [ ] AC-1 Normalize SKU/edition to the canonical product mapping.
- [ ] AC-2 Show unmatched SKUs and allow manual product mapping.
- [ ] AC-3 Re-run assessment after mapping correction (spec §Appendix A recalculate).

## Definition of done

- [ ] `dotnet test` proves manual mapping change triggers recalculate.
- [ ] honours ADR-002, spec §11.3.

## Dependencies

| Depends on | Why |
|------------|-----|
| us-01 (this feature) | line items must exist before mapping |

## Architecture decisions in force

- ADR-002 (Quotes), spec §11.3 (guardrails).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | SKU normalization + unmatched detection | M | phase-31 |
| task-02 | Manual mapping + recalculate trigger | M | phase-32 |

## Council decisions carried into this story

No target generated if normalization unresolved (spec §11.3).

## Open questions

- none.
