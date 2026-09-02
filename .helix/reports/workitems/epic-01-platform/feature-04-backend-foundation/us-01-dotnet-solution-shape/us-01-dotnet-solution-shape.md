---
id: us-01
type: user-story
parent: feature-04
wave: R0
status: active
---

# us-01-dotnet-solution-shape — .NET modular monolith solution + projects

## Story

As a **backend engineer**, I want one class-library project per bounded context plus
a shared kernel and thin API/worker hosts, so that module boundaries are compile-time
explicit and the worker reuses the same domain logic.

## Acceptance criteria

- [ ] AC-1 Projects exist for Identity/Workspace, Documents/Contracts, Suppliers/Products, Renewals, Savings, Quotes, Chat, Benchmark Service, AI Gateway, Audit, and Shared Kernel.
- [ ] AC-2 Domain projects reference only Shared Kernel and the interfaces of AI Gateway / Benchmark Service.
- [ ] AC-3 API Host and Worker Host are thin composition roots with no business logic.

## Definition of done

- [ ] `dotnet build` succeeds; an architecture test fails if a domain project references a provider SDK or another domain project's internals.

## Dependencies

| Depends on | Why |
|------------|-----|
| — | first backend slice |

## Architecture decisions in force

- ADR-002 — one project per bounded context + shared kernel + hosts; no microservices split.

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Scaffold solution + projects + architecture test | L | phase-3 |

## Council decisions carried into this story

Projects + dependency direction per ADR-002. .NET 10 LTS (assumption). In-process mediator/DI.

## Open questions

- CQ-005 (API versioning scheme) — carried in ADR-002 assumption; resolved at API surface.
