# Contigo backend

.NET 10 modular monolith + background worker (ADR-002). One class-library
project per bounded context, a shared kernel, and two thin hosts. Domain
modules never reference a provider SDK or another domain's internals —
`Contigo.ArchitectureTests` fails the build if a project reference points
the wrong way.

Honours ADR-003 (Postgres + pgvector, EF Core), ADR-009 (RLS as the
non-bypassable backstop), and ADR-005 (API + worker as Container Apps).

## Solution

```
backend/
  Contigo.slnx
  Directory.Build.props          # net10.0, nullable, TreatWarningsAsErrors
  src/
    Contigo.Api/                 # thin HTTP composition root (port 8080 in containers)
    Contigo.Worker/              # thin worker composition root
    Contigo.SharedKernel/        # TenantId, EntityId, Result<T>, IClock, IAuditWriter, IDocumentStorage
    Contigo.Identity.Workspace/  # workspace, membership, roles (live)
    Contigo.Documents.Contracts/ # upload, metadata, staged extraction pipeline (live)
    Contigo.Documents.Contracts/ # upload, metadata, extraction job, contract correction (live)
    Contigo.Audit/               # append-only audit events (live)
    Contigo.AiGateway/           # IAiGateway + LoggingAiGateway decorator — no Foundry SDK yet
    Contigo.AiGateway/           # IAiGateway + FixtureAiGateway, wired via DI — no Foundry SDK yet
    Contigo.Benchmark/           # IBenchmarkService only — fixture adapter is later (R3)
    Contigo.Suppliers.Products/  # scaffold (R1+)
    Contigo.Renewals/            # scaffold (R2)
    Contigo.Savings/             # scaffold (R3)
    Contigo.Quotes/              # scaffold (R4)
    Contigo.Chat/                # Ask Contigo structured-vs-semantic query router (R1, task E02/F04/US01/T01)
  tests/                         # per-module + architecture + R0 integration
```

Hosts are composition roots only: they register modules via `AddXxxModule`
and map HTTP / hosted services. Business logic lives in the libraries.

## Commands

Requires the .NET 10 SDK. Integration tests pull a `pgvector/pgvector:pg16`
Testcontainer (Docker must be running).

```bash
cd backend
dotnet restore Contigo.slnx
dotnet build Contigo.slnx --configuration Release
dotnet test Contigo.slnx --configuration Release
```

Local API (https://localhost:7109, http://localhost:5029 — matches
`web/public/config.json`):

```bash
dotnet run --project src/Contigo.Api/Contigo.Api.csproj --launch-profile https
```

`appsettings.Development.json` points at `localhost:5432` database
`contigo_dev` (user/password `contigo`) and Azurite
(`UseDevelopmentStorage=true`). There is no docker-compose in this repo
yet — bring your own Postgres (with `VECTOR` enabled) and Azurite, or rely
on Testcontainers inside `dotnet test`.

EF migrations live in each module that owns a DbContext
(`Contigo.Identity.Workspace`, `Contigo.Documents.Contracts`,
`Contigo.Audit`). Apply them against the same database the hosts use;
RLS policies are added in those migrations, not in Terraform.

## HTTP surface today

| Method | Path | Notes |
|--------|------|-------|
| GET | `/health` | ASP.NET health checks |
| POST | `/api/workspaces` | create workspace |
| POST | `/api/workspaces/{tenantId}/invites` | invite; roles Admin / Procurement / Legal / Finance / ReadOnly |
| POST | `/api/documents` | multipart `file` + `X-Tenant-Id` header |
| GET | `/api/documents/{id}` | metadata/status; same header |
| PATCH | `/api/contracts/{id}` | `{ corrections: { <field>: <string\|null> }, reason? }` + `X-Tenant-Id` header; versioned correction (ADR-003 `ContractVersion`/`CorrectionHistory`, ADR-009 RLS) — see `Contigo.Documents.Contracts.Application.ContractCorrectionService.CorrectableFieldNames` for the accepted field list |
| GET | `/api/audit` | tenant-scoped; expects a claims principal (integration tests inject one) |
| GET | `/api/contracts` | portfolio list; spec §8.1 columns; `X-Tenant-Id` header; optional filters `supplierId`, `status`, `risk` (Low/Medium/High/Critical), `autoRenewal`, `minAnnualSpend`, `maxAnnualSpend`, `renewalFrom`/`renewalTo` (yyyy-MM-dd) — no `category` filter yet, see `PortfolioFilter`'s doc comment; optional paging `page` (default 1), `pageSize` (default 25, max 100); response is `{ items, page, pageSize, totalCount }`, not a bare array |

**Interim auth:** document upload/read and the portfolio list take the
tenant from `X-Tenant-Id`, not from a validated JWT. ADR-010 (Entra ID /
OIDC on the API) is not wired in the host yet. Do not treat the header as
the long-term contract.
**Interim auth:** document upload/read and the contract correction PATCH
take the tenant from `X-Tenant-Id`, not from a validated JWT. ADR-010
(Entra ID / OIDC on the API) is not wired in the host yet. Do not treat
the header as the long-term contract.

The web client generates TypeScript types from
`web/openapi/contigo-api.v1.json`. The API does **not** yet self-publish
OpenAPI; that document is hand-authored and must grow with these routes.

## Worker

`Contigo.Worker` references the same application libraries as the API.
The R0 default queue is an **in-process** `InMemoryQueueConsumer` — Azure
Service Bus exists in Terraform (`modules/servicebus`) but is not consumed
here yet. Extraction / renewal / benchmark / quote handlers land with
those features.

## AI Gateway

`Contigo.AiGateway` is wired into DI by `Contigo.Documents.Contracts`'s own
`AddDocumentsContractsModule` (so both the API and Worker hosts get a
working `IAiGateway` with no host-side change). `IAiGateway` is bound to
`FixtureAiGateway` — deterministic, provider-free — until a live Foundry
endpoint exists (ADR-004/ADR-017); domain code depends only on the
interface. Per-role model ids/versions (`classify`/`extract`/`embed`/
`answer`) bind from the `AiGateway:Models` configuration section
(`AiGateway:Models:Extract:ModelId`, etc. — env var form
`AiGateway__Models__Extract__ModelId`) and default to ADR-004's candidate
models when that section is absent, so no config is required to run
locally.

`Contigo.Documents.Contracts.Application.Extraction.StagedExtractionService`
runs product spec §7.2's seven-stage pipeline (metadata → commercial terms
→ dates → price/SKU → clauses → obligations → risk) over already
page-mapped text (`DocumentPageText` — native-vs-OCR text acquisition is a
separate concern, not this service's) and persists every fact with source
span/page + confidence (spec §7.3) — directly on `ContractLineItem`/
`Clause`/`Obligation`/`Risk`, or via the new `ExtractionEvidence` table for
`Contract`'s own scalar fields. Nothing yet calls it from an HTTP endpoint
or the queue; wiring a caller is a later task.

## Containers and CI

`.github/workflows/backend.yml` (path-filtered to `backend/**`):

1. `dotnet restore / build / test` on `Contigo.slnx` (required status check).
2. On merge to `main` (or `workflow_call` for demo): Azure login via OIDC
   (`contigo-sp-<env>`), `az acr build` of
   `src/Contigo.Api/Dockerfile` and `src/Contigo.Worker/Dockerfile`, then
   `az containerapp update` of `ca-contigo-<env>-api` / `-worker`.

Images are tagged with `github.sha`. Container Apps listen on **8080**
(`ASPNETCORE_URLS=http://+:8080`). Deployed connection strings are
environment variables (`ConnectionStrings__IdentityWorkspace`,
`DocumentsContracts`, `Audit`, `Storage`) — never committed.

Image pull requires AcrPull on the workload identity; that grant is not
yet in Terraform — see [`infra/README.md`](../infra/README.md) known gaps.

## Dependency direction (ADR-002)

Allowed Contigo project references (enforced by
`tests/Contigo.ArchitectureTests`):

| Module | May reference |
|--------|----------------|
| Domain modules | `SharedKernel` only, plus `AiGateway` (Documents, Chat) or `Benchmark` (Renewals, Savings, Quotes) |
| `AiGateway` / `Benchmark` implementations | provider SDKs — when they exist; domain modules see the interface only |
| `Contigo.Api` / `Contigo.Worker` | all modules (composition roots). Azure Blob SDK is host-only |

Do not add a domain → domain or domain → Azure SDK project/package
reference to make a task compile. Put the adapter in the host or behind
the gateway/service project.
