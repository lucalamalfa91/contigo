---
id: us-01
type: user-story
parent: feature-01
wave: R1
status: active
---

# us-01-ai-gateway-classification — AI Gateway + document classification

## Story

As a **backend engineer**, I want the AI Gateway to expose role interfaces and a
document classification path, so that an uploaded document is classified into MSA/
Order Form/SOW/Amendment/Quote/Invoice/Price List/NDA/DPA/Other before extraction.

## Acceptance criteria

- [ ] AC-1 Gateway exposes ocr/classify/extract/embed/answer interfaces (config-selected model IDs).
- [ ] AC-2 Document classification returns a type and confidence, logging model/version/prompt/timestamp/input-hash.
- [ ] AC-3 Domain code calls only `IAiGateway` — never a Foundry SDK, Document Intelligence SDK, or REST client.

## Definition of done

- [ ] `dotnet test` on gateway roles passes; classification fixture returns expected type.
- [ ] honours ADR-004 (role split, cheapest per role), ADR-011 (logging, no-training), and ADR-017 (`ocr` role).

## Dependencies

| Depends on | Why |
|------------|-----|
| E01/F04/US04 (deployable-api) | gateway lives in the backend solution |

## Architecture decisions in force

- ADR-004 (role-split, config-selected IDs), ADR-011 (logging + isolation), ADR-017 (`ocr` role).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | AI Gateway role interfaces (ocr/classify/extract/embed/answer) + classification | M | phase-12 |
| task-02 | Gateway usage logging + no-training config | S | phase-13 |

## Council decisions carried into this story

AI Gateway is the only Foundry / Document Intelligence touchpoint; roles selected by config; cheapest-per-role; `ocr` is a V1 role (ADR-017).

## Open questions

- CQ-008 (model IDs) — assumption: config-selected IDs confirmed at implementation; fixture gateway acceptable until `demo`.
