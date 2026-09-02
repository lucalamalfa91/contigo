---
id: us-01
type: user-story
parent: feature-03
wave: R4
status: active
---

# us-01-negotiation-strategy — Negotiation strategy generation

## Story

As a **Procurement** user, I want an explainable negotiation strategy (opening target,
range, walk-away, levers with rationale), so that I know how to negotiate the quote.

## Acceptance criteria

- [ ] AC-1 Generate opening target, acceptable range, walk-away threshold, levers, rationale.
- [ ] AC-2 Rationale cites explicit evidence per lever (App C #2).
- [ ] AC-3 Arithmetic (target/saving) is deterministic; only language is LLM (App C #6).

## Definition of done

- [ ] `dotnet test` proves strategy generation + evidence-backed levers.
- [ ] honours ADR-002, ADR-004 (answer role), spec §12.1.

## Dependencies

| Depends on | Why |
|------------|-----|
| feature-02 (assessment) | strategy derives from target/saving |

## Architecture decisions in force

- ADR-002 (Quotes), ADR-004 (answer role), App C #2/#6.

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Strategy generation + levers + rationale | L | phase-33 |
| task-02 | Evidence citation per lever | M | phase-34 |

## Council decisions carried into this story

Strategy per spec §12.1; deterministic money + LLM language split.

## Open questions

- none.
