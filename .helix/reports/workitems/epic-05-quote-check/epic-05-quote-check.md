---
id: epic-05
type: epic
wave: R4
status: active
---

# epic-05-quote-check — New Purchase / Quote Check (R4)

## Business capability

Upload a supplier proposal, extract line items with quantities/SKU/price/discount/
terms, match them to the Benchmark Service, produce a line-level market assessment
with a recommended target range and potential saving, and generate an explainable
negotiation strategy plus outcome capture — so that **a new proposal can be assessed
in minutes** (spec §16 R4).

## Product coverage

| Source | Item |
|--------|------|
| spec §16 | R4 — Quote Check (definition of success) |
| spec §4.4 | extract, match, flag, target, strategy, correction |
| spec §11 | workflow, assessment output, guardrails |
| spec §12 | negotiation recommendation + outcome capture |
| spec §17.1 | quote → line items → match → assessment → target → strategy |
| ADR-002 | Quotes bounded context |
| ADR-003 | PostgreSQL (quote/assessment/outcome entities) |
| ADR-009 | tenancy / RLS |

## Features

| ID | Title | Wave |
|----|-------|------|
| feature-01 | quote-extraction (upload → line items → normalize) | R4 |
| feature-02 | quote-assessment (benchmark match → market position) | R4 |
| feature-03 | negotiation-strategy (target + levers + outcome) | R4 |
| feature-04 | r4-integration (Day-1 path) | R4 |

## Success looks like

On `demo` (customer Day-1 path, spec §20): a Procurement user uploads a supplier
quote PDF, gets normalized line items, a line-level market assessment against the
fixture benchmark, a recommended target range and potential saving, an explainable
negotiation strategy, and can record the final negotiated outcome for realized-savings
tracking.

## Architecture decisions in force

- ADR-002, ADR-003, ADR-009, ADR-001 (fixture adapter, no paid provider).

## Out of scope

- Live negotiation orchestration / autonomous supplier communication (spec §1.2).
- Provider-specific adapters beyond the fixture benchmark.
