---
id: E01/F04/US01/T01
type: task
story: us-01-dotnet-solution-shape
wave: R0
status: live
target_repo: contigo-backend
---

# task-01-dotnet-solution — Scaffold solution + module projects + architecture test

## Coding objective

Create the ASP.NET Core solution per ADR-002: one class-library project per bounded
context — `Contigo.Identity.Workspace`, `Contigo.Documents.Contracts`,
`Contigo.Suppliers.Products`, `Contigo.Renewals`, `Contigo.Savings`,
`Contigo.Quotes`, `Contigo.Chat`, `Contigo.Benchmark`, `Contigo.AiGateway`,
`Contigo.Audit` — plus `Contigo.SharedKernel`, and thin hosts `Contigo.Api` and
`Contigo.Worker`. Domain modules reference only `SharedKernel` and the AI Gateway /
Benchmark interfaces; an architecture test (e.g. `Microsoft.CodeAnalysis` or a
`Shouldly`+reflection test) fails the build if a domain project references a
provider SDK or another domain project's internals.

## Parent story AC covered

- AC-1 (all 11 modules + kernel + hosts)
- AC-2 (reference direction)
- AC-3 (thin hosts)

## Files to create or modify

| Path | Change |
|------|--------|
| workspace/contigo-backend/Contigo.sln | solution |
| workspace/contigo-backend/src/*/*.csproj | one project per module + hosts |
| workspace/contigo-backend/tests/Contigo.ArchitectureTests/*.cs | dependency-direction test |

## Context the implementer needs

- **Architecture decisions in force**: ADR-002 (one project per bounded context, shared kernel, thin hosts).
- **Do not touch**: infra/ web/ mobile/.

## Definition of done

- [ ] `dotnet build` exits 0; `dotnet test` (architecture) passes.

## Tests required

| Level | What it proves | Where |
|-------|----------------|-------|
| architecture | domain → provider/SDK or domain internals is blocked | `tests/Contigo.ArchitectureTests` |

## Open questions blocking this task

- none

## Wave-spec entry

```yaml
- id: E01/F04/US01/T01
  prompt: reports/workitems/epic-01-platform/feature-04-backend-foundation/us-01-dotnet-solution-shape/tasks/task-01-dotnet-solution.md
  produces: [dotnet-solution]
  depends_on: [github-org-repos]
  effort: L
  layer: backend
  status: live
```
