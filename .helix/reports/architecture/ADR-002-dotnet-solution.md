# ADR-002 — ASP.NET Core modular monolith + background worker (solution/project layout)

- **Status**: accepted
- **Date**: 2026-09-02
- **Deciders**: software-architect (owner); cloud-architect (host/runtime), security-architect (tenancy/RLS), delivery-manager (CI path filter) reconcile at council-close
- **Locked citations**: Backend — C# / ASP.NET Core (current LTS at implementation time); Modular monolith + background worker; No microservices split in V1 (locked-decisions.md).

## Context and problem statement

The brief locks a C# / ASP.NET Core LTS **modular monolith + background worker**, no microservices split in V1 (brief §7). The product spec's deployable topology names module boundaries explicitly: Identity/Workspace, Documents/Contracts, Suppliers/Products, Renewals/Savings/Quotes, Benchmark Service, and AI Gateway (spec §5.1), plus Chat and Audit (brief §7). The spec also requires that "AI is not the database" — canonical facts live in structured storage, and deterministic arithmetic/date/money calculations stay in code, not the LLM (spec Appendix C rules 1, 6; §7).

The question this ADR answers is **how** these modules are physically laid out in `backend/` such that boundaries stay explicit, the worker can reuse the same domain logic, and there is no accidental microservices split — while remaining separable later *only* when scale or team ownership requires it (spec §5.1).

## Decision drivers

- Module boundaries must follow the product spec, not a technical preference (brief §7).
- The worker must run the same domain/application logic as the API without duplication — extraction, renewal recomputation, and benchmark refresh all need to enqueue and process jobs.
- Domain modules must never call a provider directly (AI → AI Gateway; benchmark → Benchmark Service) — the gateway boundary must be a hard compile-time dependency direction.
- A future microservices split must be possible but must not be done now ("no microservices split in V1").

## Considered options

1. **One project per module + shared kernel + gateway/worker host projects** — separate class-library project per bounded context, referenced by a thin API host and a thin worker host.
2. **Single monolithic project with folders** — one ASP.NET project, modules as folders/namespaces only.
3. **One project per module, each self-hostable** — each module is an independently runnable host (mini-services), which is a de-facto microservices split.

## Decision outcome

**Chosen: Option 1** — one class-library project per bounded context, a thin Composition Root (the API host) that wires them via in-process mediator/dependency injection, and a thin Worker host that references the same libraries and consumes the same queue. This is because it makes module boundaries compile-time explicit (satisfying the spec's naming and the future-split intent) without introducing the network/process boundaries that would be a microservices split.

### Consequences

- **Good**: Module boundaries are enforced by project references (a domain module cannot reference the AI Gateway or Benchmark Service implementation, only their abstractions). The worker reuses the same application/domain libraries, so "recompute renewals" and "run extraction" are shared code, not copies. Future split = extract a module + its host without rewriting logic.
- **Bad**: More `.csproj` files to manage than a single folder project; a careless project-reference addition can still couple modules, so a code-review/architecture-test rule must police dependency direction.
- **Neutral**: API versioning, mediator choice, and ORM/access library are separate decisions (see ADR-data-store, and council-open-questions CQ-005).

## Pros and cons of the options

### Option 1 — one project per module + shared kernel + hosts
- Good: compile-time boundaries; worker reuses logic; explicit direction of dependencies; aligns with spec module naming.
- Bad: more projects; needs an architecture test to keep the layering honest.

### Option 2 — single project, folders
- Good: simplest to start; no project-reference discipline.
- Bad: boundaries are convention-only (a module can reach into another) — does not satisfy the explicit-boundary intent of spec §5.1; worker would have to reference the whole monolith.

### Option 3 — each module self-hostable
- Good: maximum independence.
- Bad: that is a microservices split by another name; explicitly against the locked "no microservices split in V1".

## Implications for the decomposition

Every task that creates or extends a domain capability must add code to the matching bounded-context project and must **not** reference a provider (AI or benchmark) directly. The AI Gateway and Benchmark Service are separate projects exposing interfaces consumed by domain modules; their implementations live behind the gateway/service project boundary. Architecture tests must fail the build if a domain project references a provider SDK or another domain project's internals. The worker host and API host share the same application services; queue message handlers belong to the worker host, not to domain projects.

## Assumptions

- "Current LTS" is .NET 10 (the LTS at the expected implementation date); final target confirmed at implementation time.
- API versioning scheme (URI prefix `/api/v1` vs header) is an open question carried in reports/open-questions.md (CQ-005 subset).
- The mediator/DI pattern is in-process; no durable outbox/messaging middleware is assumed beyond the queue at R0.
