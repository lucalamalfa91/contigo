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
    Contigo.Documents.Contracts/ # upload, metadata, hybrid OCR pre-pass, staged extraction, contract correction (live)
    Contigo.Audit/               # append-only audit events (live)
    Contigo.AiGateway/           # IAiGateway (classify/extract/embed/answer/ocr) + FixtureAiGateway + LoggingAiGateway decorator, wired via DI — no Foundry/Document Intelligence SDK yet
    Contigo.Documents.Contracts/ # upload, metadata, extraction jobs, staged extraction pipeline,
                                  # portfolio list, Contract 360, contract correction (live)
    Contigo.Audit/               # append-only audit events (live)
    Contigo.AiGateway/           # IAiGateway + FixtureAiGateway (deterministic) behind the
                                  # LoggingAiGateway decorator, wired via DI — no Foundry SDK yet
    Contigo.Documents.Contracts/ # upload, metadata, staged extraction, portfolio, contract correction + history (live)
    Contigo.Audit/               # append-only audit events (live)
    Contigo.AiGateway/           # IAiGateway + FixtureAiGateway (wired via DI) + LoggingAiGateway decorator — no Foundry SDK yet
    Contigo.Benchmark/           # IBenchmarkService only — fixture adapter is later (R3)
    Contigo.Suppliers.Products/  # scaffold (R1+)
    Contigo.Renewals/            # deterministic renewal-date / cancellation-deadline engine (R2, task E03/F01/US01/T01; live)
    Contigo.Savings/             # scaffold (R3)
    Contigo.Quotes/              # scaffold (R4)
    Contigo.Chat/                # Ask Contigo structured-vs-semantic query router (R1, task E02/F04/US01/T01) + deterministic dates/spend query handlers (task E02/F04/US01/T02) + RagAnswerService (task E02/F04/US02/T01) + AbstainGuard no-fabrication guard (task E02/F04/US02/T02); AddChatModule wired into Contigo.Api by this last task
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
| POST | `/api/documents` | multipart `file` + `X-Tenant-Id` header; also runs `DocumentProcessingPipeline` (classify → hybrid parse → staged extraction → RAG indexing) synchronously before responding (task E02/F06/US01/T01, r1-integration) — response `processingStatus`/`contractId` reflect that run's outcome, not just the initial "Uploaded" write |
| GET | `/api/documents/{id}` | metadata/status; same header |
| PATCH | `/api/contracts/{id}` | `{ corrections: { <field>: <string\|null> }, reason? }` + `X-Tenant-Id` header; versioned correction (ADR-003 `ContractVersion`/`CorrectionHistory`, ADR-009 RLS) — see `Contigo.Documents.Contracts.Application.ContractCorrectionService.CorrectableFieldNames` for the accepted field list; also writes one `IAuditWriter` entry (`contract.corrected`) |
| GET | `/api/contracts/{id}/corrections` | `X-Tenant-Id` header; field-level correction history for one contract, newest first (`Contigo.Documents.Contracts.Application.ContractCorrectionHistoryQueryService`) — 404 if the contract does not exist for the tenant, `[]` if it exists but was never corrected |
| GET | `/api/audit` | tenant-scoped; expects a claims principal (integration tests inject one) |
| GET | `/api/contracts` | portfolio list; spec §8.1 columns; `X-Tenant-Id` header; optional filters `supplierId`, `status`, `risk` (Low/Medium/High/Critical), `autoRenewal`, `minAnnualSpend`, `maxAnnualSpend`, `renewalFrom`/`renewalTo` (yyyy-MM-dd) — no `category` filter yet, see `PortfolioFilter`'s doc comment; optional paging `page` (default 1), `pageSize` (default 25, max 100); response is `{ items, page, pageSize, totalCount }`, not a bare array |
| GET | `/api/contracts/{id}` | Contract 360 aggregate; spec §8.2 header + tabs (overview, commercials, products, clauses, obligations, risks, documents, benchmark, renewal, activity); `X-Tenant-Id` header; 404 when the contract does not exist or belongs to another tenant; `benchmark`/`activity` are always empty arrays until R3/R4 — see `Contract360Result`'s doc comment |
| POST | `/api/chat/query` | Ask Contigo (spec §8.3); `{ question: string }` + `X-Tenant-Id` header; routes via `AskContigoQueryRouter`. `Semantic` questions run the real RAG pipeline (`EmbeddingRetrievalService.SearchAsync` tenant-scoped retrieval → `RagAnswerService` → `IAiGateway.AnswerAsync`) and respond `{ question, intent, canDetermine, answer, citations: [{documentId, page, section}], message }` — `citations` empty and `canDetermine: false` when authorized retrieval finds nothing (spec §8.4 "no evidence, no claim"), never a fabricated answer. `Structured` questions get an honest `canDetermine: false` + explanatory `message` — no task has yet mapped a real, tenant-scoped `Contract` row into `Contigo.Chat.Application.ContractFact` for `DeterministicQueryHandler` to run against, see that type's own doc comment |
| GET | `/api/renewals` | Renewal pipeline + insight card (spec §9.3/§10.1); `X-Tenant-Id` header; auto-renewing contracts only, most urgent first; response is `{ items, totalCount }`, each item `{ contractId, supplierId, status, renewalDate, daysUntilRenewal, annualSpend, cancellationDeadline, daysUntilCancellationDeadline, autoRenewal, action, insightCard: { facts, recommendations } }` — `insightCard.recommendations`' benchmark/savings fields (`annualUpliftPercent`, `marketPosition`, `potentialSavingsRange`) are honestly `null` until the Benchmark/Savings modules land (R3); `action`/`recommendedAction` is a deterministic urgency rule, not the full spec §9.2 Priority Score — see `Contigo.Renewals.Application.RenewalPipelineBuilder`'s own doc comment |

**Interim auth:** every endpoint above that takes an `X-Tenant-Id` header
(all except `GET /api/audit`, which already expects a claims principal)
takes the tenant from that header, not from a validated JWT. ADR-010
(Entra ID / OIDC on the API) is not wired in the host yet. Do not treat
the header as the long-term contract.

The web client generates TypeScript types from
`web/openapi/contigo-api.v1.json`. The API does **not** yet self-publish
OpenAPI; that document is hand-authored and must grow with these routes.

## Worker

`Contigo.Worker` references the same application libraries as the API.
The R0 default queue is an **in-process** `InMemoryQueueConsumer` — Azure
Service Bus exists in Terraform (`modules/servicebus`) but is not consumed
here yet, and `QueueConsumerHostedService` still never dispatches a
received message to a domain handler. Extraction runs synchronously inside
`Contigo.Api`'s `POST /api/documents` today instead (`DocumentProcessingPipeline`,
above) — not through this Worker — a documented interim choice pending a
real durable-queue producer/consumer pair. Renewal / benchmark / quote
handlers land with those features.

## AI Gateway

`Contigo.AiGateway` is wired into DI by `Contigo.Documents.Contracts`'s own
`AddDocumentsContractsModule` (so both the API and Worker hosts get a
working `IAiGateway` with no host-side change). `IAiGateway` is bound to
`FixtureAiGateway` — deterministic, provider-free — until a live Foundry /
Document Intelligence endpoint exists (ADR-004/ADR-017); domain code
depends only on the interface. Per-role model ids/versions
(`classify`/`extract`/`embed`/`answer`/`ocr`) bind from the
`AiGateway:Models` configuration section (`AiGateway:Models:Extract:ModelId`,
etc. — env var form `AiGateway__Models__Extract__ModelId`) and default to
ADR-004/ADR-017's candidate models when that section is absent, so no
config is required to run locally. The `ocr` role's page-count safety
budget (ADR-017: fail visibly, never silently truncate) is its own
`AiGateway:Ocr:MaxPagesPerDocument` section (default 300 — see
`AiGatewayOcrOptions`).

`Contigo.Documents.Contracts.Application.Extraction.HybridDocumentParsingService`
implements the hybrid OCR pre-pass (ADR-017): native text extraction
(`NativeDocumentTextExtractor` — real `DocumentFormat.OpenXml` for
DOCX/XLSX, a self-contained content-stream reader for PDF; no external PDF
library is referenced — see that class's own doc comment for why) when
sufficient, otherwise the full document (no 2-page cap) through the `ocr`
gateway role. `StagedExtractionService` runs product spec §7.2's
seven-stage pipeline (metadata → commercial terms → dates → price/SKU →
clauses → obligations → risk) over the resulting page-mapped text
(`DocumentPageText`) and persists every fact with source span/page +
confidence (spec §7.3) — directly on `ContractLineItem`/`Clause`/
`Obligation`/`Risk`, or via the `ExtractionEvidence` table for `Contract`'s
own scalar fields.

`Contigo.Documents.Contracts.Application.Extraction.DocumentProcessingPipeline`
(task E02/F06/US01/T01, r1-integration) is that caller: given the just-
uploaded bytes, it runs the hybrid parse, then `IAiGateway.ClassifyAsync`
over the resulting text (setting `Document.DocumentType` and completing
the `Classification` job `DocumentUploadService` queues at upload), then
`StagedExtractionService`, then indexes every parsed page into the
`embedding` table (see `EmbeddingRetrievalService` below) — one call proves
the whole spec §7.1 pipeline. `POST /api/documents` (`Contigo.Api.Program`)
runs it synchronously, in the same request, right after the upload itself
is durable — a deliberate interim choice (see `DocumentProcessingPipeline`'s
own doc comment): nothing in this codebase dispatches the queued
`Classification` job off a durable queue yet (`Contigo.Worker.Queue
.QueueConsumerHostedService` still never dispatches a received message to a
domain handler — see the Worker section below), so synchronous/in-request
is the smallest honest way to make R1's "upload → ... → Ask Contigo" promise
true on `dev`/`demo` today. A pipeline failure never turns an already-
successful upload into an HTTP error — it is recorded on the `Document`/
`ExtractionJob` rows and reported in the response, same as any other
per-stage failure in this pipeline.

`Contigo.Documents.Contracts.Application.EmbeddingRetrievalService`
(us-02-embedding-search-index) is the pgvector half of Ask Contigo RAG:
`IndexChunkAsync` embeds a text chunk via `IAiGateway.EmbedAsync` and
persists it to the `embedding` table; `SearchAsync` embeds a query the
same way and returns the tenant's nearest chunks by cosine distance
(`Vector.CosineDistance`), explicitly filtered by `tenant_id` on top of
that table's own RLS policy. Embedding generation never touches a
provider SDK directly — always through `IAiGateway`. `SearchAsync`'s first
caller is `POST /api/chat/query` (task E02/F04/US02/T01, below).
`IndexChunkAsync`'s first production caller is `DocumentProcessingPipeline`
(task E02/F06/US01/T01, r1-integration, above) — one `Embedding` row per
parsed page, `SourceType="Document"`/`SourceId=<documentId>`, so a document
is retrievable for Ask Contigo immediately after it finishes processing. A
tenant that has never uploaded anything (or whose upload is still
processing/failed) still honestly returns "cannot determine" — there is
simply nothing indexed for it yet, not a bug.

## Ask Contigo — query router + deterministic queries + RAG citations

`Contigo.Chat.Application.AskContigoQueryRouter` classifies a natural-language
question (product spec §8.3) as `Structured` (deterministic query/filter, no
LLM) or `Semantic` (needs RAG retrieval) — task E02/F04/US01/T01.
`DeterministicQueryPlanner` + `DeterministicQueryHandler` (task
E02/F04/US01/T02) turn a `Structured` decision into an actual answer for the
two families spec §8.3 names as "dates" and "spend": "which contracts renew
in the next N days" (a filter on `Contract.AutoRenewal`/`EndDate`) and
"what is our annual spend [with a supplier]" (a sum of `Contract.AnnualSpend`).
No supplier-name -> `SupplierId` resolution exists yet (Suppliers/Products is
still an empty scaffold — the same root cause as the portfolio list's missing
`category` filter above), so a question that names a specific supplier (for
example "What is our Microsoft annual spend?") is still summed across
**every** supplier today; `DeterministicQueryResult.SupplierScopeUnresolved`
is `true` whenever that happened, so a caller can tell "$700,000 total" apart
from "$700,000 with Microsoft" instead of presenting one as the other.
A structured question outside those two families (for example "total
contract value") is reported as `Unsupported` rather than answered against
the wrong field.

`Contigo.Chat.Application.RagAnswerService` (task E02/F04/US02/T01,
us-02-rag-citations, AC-1/AC-2/AC-3) turns a `Semantic` decision plus
already-retrieved, already-authorized evidence into a grounded answer with
citations via `IAiGateway.AnswerAsync` (ADR-004 `answer` role) — citations
or an explicit "cannot determine" (spec §8.4 "no evidence, no claim"), never
a fabricated answer. It also writes one `IAuditWriter` entry per successful
call (`chat.answered` — ADR-011 "audit of access"), never the raw
question/evidence/answer text.

`Contigo.Chat.Application.AbstainGuard` (task E02/F04/US02/T02, abstain-guard)
is the no-fabrication guard `RagAnswerService.AnswerAsync` runs on every
gateway result before it is audited or returned: a "cannot determine" result
passes straight through, but a "determined" result is only trusted when it
carries at least one citation, has non-empty answer text, and every citation's
`DocumentId` matches one of the evidence documents actually handed to the
gateway — otherwise the guard downgrades it to an honest "cannot determine"
(preserving the original `AiCallMetadata` for reproducibility) rather than let
an unsupported or hallucinated citation through (Appendix C rules 2 and 10).
`FixtureAiGateway` can never trigger this — it only ever echoes citations
built from its own input evidence — so today the guard is a no-op in practice;
it exists for the Foundry-backed `IAiGateway` implementation ADR-004
anticipates, which can hallucinate. The audit detail line gains one field,
`abstainGuardIntervened=true|false`, so an operator can see a caught
fabrication attempt without the guard silently discarding the signal — the
free-text reason itself is deliberately not logged (ADR-011: no model
output/content in audit rows).

`Contigo.Chat` cannot reference `Contigo.Documents.Contracts` (see
"Dependency direction" below), so neither `DeterministicQueryHandler` nor
`RagAnswerService` retrieves anything itself: both operate on caller-supplied
data (`ContractFact` / a pre-retrieved evidence list respectively) — small
DTOs/parameters the module owns or accepts, never the real `Contract`/
`Embedding` entities. `Contigo.Api.ChatEndpointExtensions` (`POST
/api/chat/query`, task E02/F04/US02/T01) is the composition root that closes
this gap for the `Semantic` branch: it resolves the tenant, calls
`EmbeddingRetrievalService.SearchAsync` (auth-before-retrieval, ADR-011),
maps each hit into `Contigo.AiGateway.Contracts.AiEvidenceSnippet`, then
calls `RagAnswerService`. `DocumentId` on that mapping is a
`{SourceType}:{SourceId}` composite (not a bare id): an `Embedding` row's
`SourceId` only really identifies a document when `SourceType` is
`"Document"` — for `"Clause"`-sourced evidence it identifies the clause row,
and silently relabelling one as the other would misattribute the citation.
`Page` is left `null` (no page column on `Embedding` yet) and `Section`
reports the real chunk index instead of a fabricated section title — true
page/section resolution (joining back to `Clause.SourcePage`/`SourceSpan`) is
a follow-up gap, not attempted by this task. No task has yet mapped a real,
tenant-scoped `Contract` row into `ContractFact`, so the endpoint's
`Structured` branch reports an honest "not wired yet" instead of guessing.

## Renewal Intelligence — deterministic renewal engine

`Contigo.Renewals.Application.RenewalEngine` (task E03/F01/US01/T01,
us-01-deterministic-dates) is product spec §9.1's "calculate renewal date,
calculate cancellation deadline, calculate days remaining" made concrete:
pure, synchronous arithmetic over a `ContractRenewalTerms` snapshot — no
database, no HTTP call, no LLM call (Appendix C rule 6) — returning a
`RenewalCalculationResult` with a three-way `RenewalCalculationStatus`:

- `Determined` — `RenewalDate` equals `EndDate` when `AutoRenewal` is true
  (the same convention `PortfolioListItem.RenewalDate` /
  `Contract360Header.RenewalDate` already use, reproduced here on purpose).
  `CancellationDeadline` additionally needs `CancellationNoticeDays`
  (`EndDate` minus that many days) and can stay null even inside a
  `Determined` result when that one input is missing or negative — a
  renewal date and its cancellation deadline are independently
  determinable.
- `NoRenewal` — `AutoRenewal` is false: a known fact, not a data gap, so it
  is not folded into `CannotDetermine`.
- `CannotDetermine` — `EndDate` itself is unknown: nothing can be computed
  without fabricating it (Appendix C rule 10; parent story AC-3).

`DaysUntilRenewal`/`DaysUntilCancellationDeadline` are signed, unclamped
day counts relative to `IClock.UtcNow` — a negative value honestly means
the date already passed, rather than being hidden behind a floor of zero.
`RenewalEngine.CalculateMany` is the batch form for spec §9.1's "daily
scheduler for each active contract" shape; deciding which contracts are
"active" (in scope to call it with) is the caller's job, not the engine's.

`ContractRenewalTerms` deliberately does not reference
`Contigo.Documents.Contracts.Domain.Contract` — ADR-002 forbids
`Contigo.Renewals` from referencing `Contigo.Documents.Contracts` at all
(same reason `Contigo.Chat.Application.ContractFact` is its own small DTO,
not the real `Contract` entity).

`Contigo.Renewals.Application.RenewalPipelineBuilder` (task E03/F03/US01/T01,
us-01-renewal-dashboard-api) is `RenewalEngine`'s first real caller and backs
`GET /api/renewals` (see the HTTP surface table above): it turns a batch of
`RenewalDashboardCandidate` (another small DTO, the same dependency-direction
shape as `ContractRenewalTerms`) into a pipeline row plus a facts/
recommendations insight card (spec §9.3), ordered most-urgent-first by days
until the relevant date. `Contigo.Api.RenewalsEndpointExtensions` is the
composition root that maps a real, tenant-scoped `PortfolioListItem`
(Documents/Contracts) onto `RenewalDashboardCandidate` — the one mapping
neither module may do itself, same pattern `ChatEndpointExtensions` already
uses for `EmbeddingSearchResult` → `AiEvidenceSnippet`. `AddRenewalsModule`
is now called by `Contigo.Api`'s `Program.cs` — the same "wiring lands with
the first real caller" sequencing `AddChatModule` followed before
`Contigo.Chat` had one. `Contigo.Worker`'s `Program.cs` still does not call
it — no worker job (the renewal-opportunity generation / cancellation-alerts
threshold scheduler wave-spec tasks) depends on `renewal-engine` yet.

One honest gap remains, deliberately out of this task's file scope:
`Contract` has no persisted `CancellationNoticeDays` column — its "dates"
extraction stage (`StagedExtractionService.ApplyDatesFact`) still writes a
raw `cancellationDeadline` date directly from extraction instead of a
notice-period day count (product spec §7.3's own extraction-evidence example
names `cancellation_notice_days`, not a computed date), so `RenewalEngine`
itself still cannot derive a cancellation deadline for any real contract.
`RenewalPipelineBuilder` works around this for the dashboard specifically by
carrying `Contract.CancellationDeadline` (the already-extracted raw fact)
straight through as its own field, independent of `RenewalEngine`'s
notice-day derivation — see `RenewalDashboardCandidate.CancellationDeadline`'s
own doc comment. Giving extraction a real `CancellationNoticeDays` field (so
`RenewalEngine.Calculate` itself can derive the deadline, the way it already
derives `RenewalDate`) is follow-up work in `Contigo.Documents.Contracts`, a
different module and a different task's file scope.

## R1 demo smoke test

The automated proof of task E02/F06/US01/T01 (r1-integration) is
`dotnet test` — `Contigo.IntegrationTests.R1EndToEndTests` (AC-1/AC-2/AC-4:
upload → parse/OCR → classify → extract → portfolio → 360 → Ask Contigo
with citations → correction, plus a scanned/image fixture through the
`ocr` gateway role) and `R1CrossTenantIsolationTests` (AC-3, across the
whole path). To manually smoke-test the same path against a running
`dev`/`demo` deployment:

```bash
API=https://<api-host>
TENANT=$(curl -s -X POST "$API/api/workspaces" -H 'Content-Type: application/json' \
  -d '{"name":"Smoke Test Co"}' | jq -r .id)

DOC=$(curl -s -X POST "$API/api/documents" -H "X-Tenant-Id: $TENANT" \
  -F "file=@contract.pdf;type=application/pdf" | jq -r .id)

# processingStatus/contractId reflect DocumentProcessingPipeline's own run
# (classify -> hybrid parse -> staged extraction -> RAG indexing) -- POST
# /api/documents runs it synchronously before responding.
curl -s "$API/api/documents/$DOC" -H "X-Tenant-Id: $TENANT" | jq .
CONTRACT=$(curl -s "$API/api/documents/$DOC" -H "X-Tenant-Id: $TENANT" | jq -r .contractId)

curl -s "$API/api/contracts" -H "X-Tenant-Id: $TENANT" | jq .
curl -s "$API/api/contracts/$CONTRACT" -H "X-Tenant-Id: $TENANT" | jq .

curl -s -X POST "$API/api/chat/query" -H "X-Tenant-Id: $TENANT" \
  -H 'Content-Type: application/json' -d '{"question":"What does this contract cover?"}' | jq .
```

Honest caveat: `IAiGateway` still binds to `FixtureAiGateway` (no live
Foundry/Document Intelligence endpoint exists yet, ADR-004/ADR-017) and its
`ExtractAsync` always returns an empty `{}` — a real `demo` upload today
lands `NeedsReview` with zero extracted facts. This smoke path proves the
*pipeline wiring* end-to-end (every stage runs, links, and is queryable),
not extraction accuracy; `R1EndToEndTests` proves the persistence/HTTP
contract against a scripted gateway that returns real, schema-shaped facts.

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

Image pull uses this environment's workload identity (`AcrPull` on
`modules/acr`, `registry {}` on `modules/containerapps`). Confirm the
HCP VCS apply on `contigo-<env>` before the first `az containerapp update`
to that registry, or the revision fails with ACR `UNAUTHORIZED`.

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
