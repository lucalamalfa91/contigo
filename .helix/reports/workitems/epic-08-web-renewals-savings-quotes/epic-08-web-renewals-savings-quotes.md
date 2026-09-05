---
id: epic-08
type: epic
wave: 8
layer: web
status: active
---

# epic-08-web-renewals-savings-quotes — Web renewals, savings, quote check (R2–R4 UI) + Day-1 integration

## Business capability

Deliver the R2–R4 user-visible ladder in the browser — the renewal pipeline
(threshold strip, priority table, insight card + action), the Home savings KPIs
+ opportunities, and the quote-check stepper (Extract → Assessment → Target →
Negotiation → outcome) — then close the pass with a single `final-integration`
walk of the full Day-1 path (§20) in the browser on `demo` (spec §16 R2–R4).

## Product coverage

| Source | Item |
|--------|------|
| spec §16 | R2 Renewals, R3 Savings, R4 Quote Check (definitions of success) |
| spec §20 | deadlines, benchmarks, prioritized savings, quote→negotiation→outcome |
| spec §9.1–9.3 | renewals |
| spec §10.1 | savings KPIs |
| spec §11.1–11.3, §12.1–12.2 | quote check + negotiation |
| ADR-018 | routes /renewals, / (home), /quotes/:id |
| ADR-020 | screens 8, 9, 10 |

## Features

| ID | Title | Wave |
|----|------|------|
| feature-01 | renewal-pipeline-ui | 8 |
| feature-02 | savings-ui | 8 |
| feature-03 | quote-check-ui | 8 |
| feature-04 | r4-web-integration | 8 |

## Success looks like

A procurement user, after the final slice is promoted (`demo-v*`, ADR-016),
completes product-spec §20 **in the browser**: upload → review → Contract 360 →
Ask → renewals action → savings opportunity → quote check → record outcome →
realized savings updates on Home — matching `inputs/design/prototypes/day1-demo.html`, not `dotnet test`, not a Swagger page.

## Architecture decisions in force

- ADR-012, ADR-016, ADR-018, ADR-019, ADR-020. Consumes `inputs/design/prototypes/screens.md` (8–10) and `day1-demo.html`.

## Out of scope

- Backend capability logic (renewal engine, benchmark, quote extraction are E03/E04/E05 done); this epic authors consuming screens + the browser Day-1 integration only.
