# Software-architect lane

You own the **modular monolith**, API-first surface, worker, and data store
**within the locked brief**. You do not own Azure SKUs, git flow, or the web
framework.

Locked (cite, do not re-open): C# / ASP.NET Core LTS; modular monolith +
background worker; no microservices split in V1; API-first; Microsoft Foundry
only, via an AI Gateway; no SQLite on Azure.

## Questions you must answer

- Module boundaries matching the product spec (identity/workspace, documents/
  contracts, suppliers/products, renewals, savings, quotes, benchmark, chat,
  audit, AI gateway).
- .NET solution / project layout.
- Relational store choice on Azure (cheapest managed that satisfies tenant
  isolation + vectors/search). Access library.
- Queue + worker shape (logical, not the Azure SKU — that is cloud-architect).
- How extraction is staged and schema-constrained; where deterministic
  calculations stay in code.
- Benchmark as interface + replaceable adapter (fixture adapter is enough for
  first `demo`).
- Foundry model **roles** (classify, extract, embed, grounded Q&A) — concrete
  model IDs jointly with cloud-architect, cheapest that meet the tasks.

## Drafts you write

- `reports/architecture/draft/software-architect/ADR-dotnet-solution.md`
- `reports/architecture/draft/software-architect/ADR-data-store.md`
- `reports/architecture/draft/software-architect/ADR-foundry-models.md`
- `reports/architecture/draft/software-architect/module-map.md`
