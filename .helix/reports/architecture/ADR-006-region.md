# ADR-006 — Azure region for `dev` and `demo`

- **Status**: accepted
- **Date**: 2026-09-01
- **Deciders**: cloud-architect (owner)
- **Locked citations**: Cloud — Microsoft Azure; Environments — `dev` and `demo` in the same region (brief §4: "Region: council (keep `dev` and `demo` in the same region)").

## Context and problem statement

Both Azure environments must live in one region to keep latency, data-residency reasoning, and Terraform plumbing identical, while honoring the cost guideline and no-multi-region constraint. The brief does not prescribe a region; the council must pick one that (a) supports all the SKUs in ADR-azure-skus — notably Container Apps consumption, PostgreSQL Flexible Server Burstable with `pgvector`, Foundry model serving, and Service Bus Standard — and (b) remains cheap and low-latency for the stakeholder-facing `demo`.

## Decision drivers

- **Service coverage**: the region must host Container Apps (consumption), Flexible Server with pgvector, Foundry model endpoints for the cheapest models, Service Bus Standard, and Entra ID.
- **Latency / reach**: `demo` is stakeholder-facing; prefer a region with good general latency to the expected audience without paying multi-region cost.
- **Cost stability**: pick a region with stable pricing for the chosen SKUs; avoid newly launched regions with incomplete feature coverage.

## Considered options

1. **West Europe (westeurope)** — mature region, full coverage of all required services.
2. **North Europe (northeurope)** — equivalent maturity, slightly different peering/latency profile.
3. **East US (eastus)** — mature and full-coverage but transatlantic latency for a Europe-based team/stakeholders.

## Decision outcome

**Chosen: Option 1 — West Europe (`westeurope`)**, because it provides full and mature coverage of every required service (Container Apps consumption, PostgreSQL Flexible Server + pgvector, Foundry model endpoints, Service Bus, Entra ID) at standard pricing while keeping stakeholder-facing `demo` latency reasonable for a European audience — all within the no-multi-region, cheapest-SKU mandate.

### Consequences

- **Good**: single region for both envs keeps Terraform, remote state, and identity wiring identical; every required SKU is available; no multi-region spend.
- **Bad**: regional outages affect both `dev` and `demo` simultaneously (acceptable for a no-prod V1); single region means no provider diversity.
- **Neutral**: Foundry model IDs must be confirmed as available in `westeurope` at implementation time (jointly with software-architect on CQ-008).

## Pros and cons of the options

### west europe
- Good: full, mature service + model coverage; stable pricing; low latency for EU stakeholders.
- Bad: single-region (no geo-redundancy).

### north europe
- Good: near-identical coverage.
- Bad: no material advantage over west europe for this product's audience; would add no benefit worth diverging.

### east us
- Good: full coverage.
- Bad: transatlantic latency for `demo`; no cost advantage.

## Implications for the decomposition

- All Terraform providers/remote-state must pin `location = "West Europe"` (or the canonical `westeurope`) for both `dev` and `demo`.
- Any task that confirms Foundry model IDs must check `westeurope` availability and price before pinning (CQ-008).
- Do not introduce a second region for either environment.

## Assumptions

- All services named in ADR-azure-skus are GA in `westeurope` (Container Apps consumption, PostgreSQL Flexible Server with `pgvector`, Service Bus Standard, Foundry model serving).
- The primary `demo` audience is European, making `westeurope` the latency-correct choice.
