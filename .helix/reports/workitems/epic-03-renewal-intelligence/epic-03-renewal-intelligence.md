---
id: epic-03
type: epic
wave: R2
status: active
---

# epic-03-renewal-intelligence — Renewal Intelligence (R2)

## Business capability

Compute deterministic renewal dates and cancellation deadlines from validated
structured contract data, generate renewal opportunities with configurable
threshold alerts, and surface a prioritized renewal pipeline with explainable
priority scores — so that **Procurement does not miss material renewal windows**
(spec §16 R2). Dates and money stay in code, never in the LLM (Appendix C #6).

## Product coverage

| Source | Item |
|--------|------|
| spec §16 | R2 — Renewals (definition of success) |
| spec §4.2 | renewal dates, cancellation deadlines, thresholds, priority |
| spec §9 | renewal generation, priority score, insight card |
| spec §17.1 | deterministic calculation; threshold events; no invented dates |
| ADR-002 | Renewals bounded context |
| ADR-003 | PostgreSQL (renewal entities) |
| ADR-009 | tenancy / RLS |
| ADR-016 | promotion dev→demo |

## Features

| ID | Title | Wave |
|----|-------|------|
| feature-01 | renewal-engine (deterministic dates + priority) | R2 |
| feature-02 | cancellation-alerts (threshold events) | R2 |
| feature-03 | renewal-dashboard (pipeline + insight card) | R2 |
| feature-04 | r2-integration | R2 |

## Success looks like

On `demo`: every active contract with validated dates has a deterministic renewal
date and cancellation deadline; threshold windows fire alerts; the dashboard ranks
renewals by an explainable priority score and recommends an action without ever
inventing a date.

## Architecture decisions in force

- ADR-002, ADR-003, ADR-009, ADR-016.

## Out of scope

- Savings/benchmark price comparison (R3) — R2 only composes time urgency and contract risk.
- Quote check and negotiation (R4).
