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
    Contigo.Benchmark/           # IBenchmarkService.GetBenchmarkAsync + normalized Contracts DTOs (E04/F01/US01/T01); BenchmarkAdapterRegistry + AddBenchmarkModule (E04/F01/US01/T02); FixtureBenchmarkAdapter registered as the default IBenchmarkProviderAdapter, incl. statistical weak-comparable abstain (E04/F01/US02/T01+T02) — no host calls AddBenchmarkModule yet (R3)
    Contigo.Suppliers.Products/  # scaffold (R1+)
    Contigo.Renewals/            # renewal engine + opportunity + explainable priority score + threshold scheduler + dashboard pipeline + action (R2; live) — see "Renewal Intelligence" below
    Contigo.Savings/             # price normalization + percentile/target/savings-range calculator (R3; task E04/F02/US01/T01) + persisted, trackable SavingsOpportunity + GET/PATCH /api/savings (task E04/F02/US02/T01) — see "Savings Intelligence" below
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
`Contigo.Audit`, `Contigo.Renewals`, `Contigo.Savings`). Apply them against
the same database the hosts use; RLS policies are added in those
migrations, not in Terraform.

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
| GET | `/api/renewals/{contractId}/priority` | Explainable priority-score breakdown for one contract (spec §9.2; story us-02-priority-score AC-1/AC-2, task E03/F01/US02/T02); `X-Tenant-Id` header; 404 when the contract does not exist or belongs to another tenant (same rule as `GET /api/contracts/{id}`); response is `{ contractId, totalScore, components: { spendWeight, timeUrgency, benchmarkOpportunity, priceIncreaseRisk, contractRisk } }`, each component `{ score, explanation }` — component weights are configurable, see `Contigo.Renewals.Configuration.PriorityScoreWeightsOptions` below; `priceIncreaseRisk`/`benchmarkOpportunity` use their honest no-data default (minimum / neutral respectively) since no uplift or benchmark-position data is wired to real contracts yet |
| POST | `/api/renewals/{id}/action` | Updates owner/status/action for one renewal (spec Appendix A; story us-01-renewal-dashboard-api AC-3); `X-Tenant-Id` header; `{id}` is the same `contractId` the GET above returns per row, not a separate stored "renewal" id; body `{ owner, status, action }` — `status` is one of `NotStarted`/`InProgress`/`Completed`; upserts one row (never a second for the same contract) and writes one `IAuditWriter` entry (`renewal.action_updated`); 400 (not 404) for a missing/invalid tenant header or route id, or for an empty `owner`/`action`/unrecognized `status` — see `Contigo.Renewals.Application.RenewalActionService`'s own doc comment for the honest gap this leaves (no check that `{id}` names an existing, tenant-owned contract; `Contigo.Renewals` cannot reference `Contigo.Documents.Contracts` at all) |
| GET | `/api/savings` | Lists the caller's tenant-scoped `SavingsOpportunity` rows, newest identified first (spec §4.3/§6; module-map.md "Savings \| SavingsOpportunity, RealizedSavings \| /api/savings"; story us-02-savings-opportunity AC-1, task E04/F02/US02/T01); `X-Tenant-Id` header; response `{ items, totalCount }`; no filters yet — see `Contigo.Savings.Application.SavingsOpportunityService.ListAsync`'s own doc comment |
| PATCH | `/api/savings/{id}` | Updates `owner`, `status` (`Identified`/`InProgress`/`Realized`) and/or `realizedAmount` on one `SavingsOpportunity` (AC-1 "updates status/owner..."; AC-3 "realized value is captured and audit-tracked", task E04/F02/US02/T02); `X-Tenant-Id` header; body `{ owner?, status?, realizedAmount? }` — a genuine partial update, any subset of the three fields; 404 when `{id}` does not name an opportunity for this tenant, 400 for every other validation failure (empty owner, unrecognized status, a negative `realizedAmount`, a `realizedAmount` combined with an explicit `status` other than `Realized`, or none of the three fields supplied); writes one `IAuditWriter` entry per successful call — `savings_opportunity.updated`, or `savings_opportunity.realized` instead when `realizedAmount` was supplied (never both). Supplying `realizedAmount` also inserts a new, append-only `Contigo.Savings.Domain.RealizedSavings` row (in the opportunity's own `currency`) and finalizes `status` as `Realized` — either because the caller's own explicit `status` already said so, or automatically when `status` was omitted (see `SavingsOpportunityService.UpdateAsync`'s own doc comment). The response's `realizedAmount` field is non-`null` only on the call that just recorded one — it is not a rolled-up read of this opportunity's full realized-value history, see `SavingsOpportunityResult.RealizedAmount`'s own doc comment |
| PATCH | `/api/savings/{id}` | Updates `owner` and/or `status` (`Identified`/`InProgress`/`Realized`) on one `SavingsOpportunity` (AC-1 "updates status/owner..."); `X-Tenant-Id` header; body `{ owner?, status? }` — a genuine partial update, either or both fields; 404 when `{id}` does not name an opportunity for this tenant, 400 for every other validation failure (empty owner, unrecognized status, or neither field supplied); writes one `IAuditWriter` entry (`savings_opportunity.updated`) per successful call — setting `status` to `Realized` here does **not** yet create an audit-tracked realized-value record, see `Contigo.Savings.Domain.SavingsOpportunityStatus.Realized`'s own doc comment for the gap task E04/F02/US02/T02 (`RealizedSavings`) closes |
| GET | `/api/savings/kpis` | Procurement-homepage KPI row (spec §4.3/§10.1; story us-01-savings-kpis AC-1, task E04/F03/US01/T01); `X-Tenant-Id` header; response `{ annualSpendAnalyzed: [{ currency, amount, contractCount }], contractsAnalyzedCount, savingsIdentified/savingsInProgress/savingsRealized: [{ currency, low, high, count, averageConfidence }], upcomingRenewalsCount }` — every money value is grouped by currency, never summed across currencies (no exchange-rate service exists anywhere in this codebase); `contractsAnalyzedCount` counts contracts whose linked document reached `DocumentProcessingStatus.Completed` (a `Contract` row can exist before that — see `Contigo.Documents.Contracts.Application.PortfolioAnalysisCalculator`'s own doc comment); `savingsRealized` reflects each opportunity's own estimated range, not yet the separate, audit-tracked `RealizedSavings` value (task E04/F02/US02/T02's own gap, see `SavingsOpportunityStatus.Realized`'s doc comment); `upcomingRenewalsCount` is the same auto-renewing-contract count `GET /api/renewals`'s own `totalCount` already reports (same 100-contract-per-tenant cap) — see `Contigo.Api.SavingsKpiEndpointExtensions`'s own comment for why it is not a second, independently-computed number |

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
real durable-queue producer/consumer pair. Benchmark / quote handlers land
with those features; renewal threshold scheduling (task E03/F02/US01/T01)
is the first of the four `13.3 Background jobs` categories this host
actually runs — see "Renewal threshold scheduler" above for
`RenewalThresholdSchedulerHostedService` and its own honest gap (no real
cross-tenant contract source wired yet).

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

## Benchmark Service

`Contigo.Benchmark.IBenchmarkService.GetBenchmarkAsync` (task E04/F01/US01/T01)
is the normalized `getBenchmark` contract product spec §10.3 names — P25/P50/P75
plus metric/currency/confidence/source/updated/comparison, so Renewals/Savings/
Quotes never depend on a provider schema. Task E04/F01/US01/T02 adds
`Contigo.Benchmark.BenchmarkAdapterRegistry`, the pluggable
`IBenchmarkProviderAdapter` registry behind that interface, wired into DI by
`Contigo.Benchmark.ServiceCollectionExtensions.AddBenchmarkModule` — it
config-selects the active adapter by name (`Benchmark:Adapter:ActiveAdapter`,
env var form `Benchmark__Adapter__ActiveAdapter`, default `"fixture"` —
`BenchmarkAdapterOptions`), the same "config-selected, swap without a code
change" convention `AiGatewayModelOptions` already uses for ADR-004.

Task E04/F01/US02/T01 (story us-02-fixture-adapter) added the first concrete
adapter as a class, but that task's own wave-spec phase ran alongside this
registry task (parallel, neither depends on the other), so it could not
register what it had just written — the adapter existed and was directly
unit-testable, but unreachable through `AddBenchmarkModule()`. Task
E04/F01/US02/T02 (fixture-confidence) closes that gap: only a concrete
adapter may ever reference a provider SDK — `Contigo.Benchmark`'s own project
file still carries none, and `Contigo.ArchitectureTests.DependencyDirectionTests
.Benchmark_module_must_not_reference_provider_sdks` fails the build if that
changes without an adapter to justify it — and now that adapter is actually
wired in. A host that calls `AddBenchmarkModule()` today gets a real,
resolvable `IBenchmarkService` (`BenchmarkAdapterRegistry`) whose default
configuration dispatches to a genuine, fixture-backed result; an unrecognized
configured adapter name (for example a `Benchmark:Adapter:ActiveAdapter`
naming a paid provider that has not been registered) still fails honestly
rather than fabricating one (ADR-001).
`Contigo.Benchmark.Fixtures.FixtureBenchmarkAdapter` (task E04/F01/US02/T01,
us-02-fixture-adapter) is that first `IBenchmarkService`/`IBenchmarkProviderAdapter`
implementation — deterministic and provider-free, backed by a hand-curated,
in-memory catalog of illustrative SaaS supplier/product comparables (never
Tropic, Vendr, or any paid market API — ADR-001, spec §10.2's "Strategic
requirement"). It registers under the name `"fixture"`
(`Configuration.BenchmarkAdapterOptions.DefaultAdapterName`), so
`BenchmarkAdapterRegistry` finds it with no separate name to keep in sync.
`GetBenchmarkAsync` requires a fixture to match on supplier, product,
geography, currency, contract term, quantity tier and a purchase-date
refresh window — seven of spec §10.4's eleven named comparison dimensions,
always more than supplier name alone — plus SKU as an optional,
confidence-boosting eighth. A fixture that clears every required dimension
*and* carries at least `FixtureBenchmarkAdapter.MinimumViableSampleSize`
comparables (task E04/F01/US02/T02: 10) returns P25/P50/P75 with a
sample-size-scaled confidence score (`Contigo`'s own score, spec §10.3 —
saturates at a sample size of 50); anything weaker — including a fixture that
matches every dimension but is too statistically thin to trust (task
E04/F01/US02/T02's own "weak-comparable abstain" objective) — returns the
explicit "insufficient market data" outcome (`Distribution: null`) instead of
a fabricated number (ADR-001; spec §10.4's benchmark-trust rule, verbatim: "a
precise-looking number from weak comparables is more dangerous than an
explicit 'insufficient market data' result"), falling back to a
same-supplier/same-product comparable's metric and sample size when one
exists so the caller still sees real (if insufficient) provenance.

`ServiceCollectionExtensions.AddBenchmarkModule` wires `IBenchmarkService` to
`BenchmarkAdapterRegistry`, which now dispatches to this adapter by default
(task E04/F01/US02/T02) — but no host calls `AddBenchmarkModule` yet, the
same "wiring lands with the first real caller" gap `Contigo.Savings` (price
normalization exists — task E04/F02/US01/T01 — but no DI registration of its
own yet) will close, and `Contigo.Renewals`'s own
`RenewalPriorityInputs.BenchmarkMarketPositionPercent` (see "explainable
priority score" below) still has no real producer wired to it either.

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
not the real `Contract` entity). Two honest gaps follow, both deliberately
out of this task's file scope:

1. No host endpoint or worker job calls `RenewalEngine` yet.
   `AddRenewalsModule` exists (`Infrastructure/ServiceCollectionExtensions.cs`)
   so the remaining tasks that depend on `renewal-engine` in the wave-spec DAG
   (priority score, the cancellation-alerts threshold scheduler) can resolve
   it from a container, but `Contigo.Api`/`Contigo.Worker`'s `Program.cs` do
   not call it yet — the same "wiring lands with the first real caller"
   sequencing `AddChatModule` followed before `Contigo.Chat` had one (see
   that section above).
2. `Contract` has no persisted `CancellationNoticeDays` column — its "dates"
not the real `Contract` entity). One of the two gaps this section used to
describe is now closed (see "Renewal threshold scheduler" below); the other
remains, deliberately out of that task's file scope too:

1. `Contract` has no persisted `CancellationNoticeDays` column — its "dates"
   extraction stage (`StagedExtractionService.ApplyDatesFact`) still writes
   a raw `cancellationDeadline` date directly from extraction instead of a
   notice-period day count (product spec §7.3's own extraction-evidence
   example names `cancellation_notice_days`, not a computed date). Mapping
   a real `Contract` row onto `ContractRenewalTerms` — and giving
   extraction a real `CancellationNoticeDays` field to populate — is
   follow-up work in `Contigo.Documents.Contracts`, a different module and
   a different task's file scope.
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

`Contigo.Renewals.Application.RenewalOpportunityGenerator` (task
E03/F01/US01/T02, us-01-deterministic-dates, the wave-spec's
`renewal-opportunity` artifact) is the next daily-scheduler step from spec
§9.1: "create/update renewal opportunity", built directly on top of
`RenewalEngine.Calculate`'s output. `Generate`/`GenerateMany` take the same
`ContractRenewalTerms` shape `RenewalEngine` does (constructor-injected, so
`AddRenewalsModule` resolves both from one container); the static
`FromCalculation` exposes the mapping rule alone for a caller that already
ran the engine itself. Three-way `RenewalOpportunityStatus` mirrors
`RenewalCalculationStatus` case-for-case (`NoRenewal`/`CannotDetermine` keep
the same names; `Determined` becomes `Open` — an opportunity Procurement has
something to act on) so a `CannotDetermine` calculation never turns into a
fabricated opportunity — it abstains the same way, per parent story AC-3.
Deliberately out of scope here, each a later task's own file: a priority
score/component breakdown (us-02-priority-score), a threshold-alert flag
(feature-02-cancellation-alerts), an owner/status/action
(feature-03-renewal-dashboard's renewal-action task, spec Appendix A `POST
/api/renewals/{id}/action`), and persistence — spec §9.1 says "create/update"
(upsert semantics) but no task has given `Contigo.Renewals` a `DbContext` yet,
so today `RenewalOpportunity` is an in-memory value, not a stored row.
## Renewal Intelligence — explainable, tunable priority score
score/component breakdown (us-02-priority-score) and a threshold-alert flag
(feature-02-cancellation-alerts) remain follow-up work. The other two gaps
this paragraph used to list here are now closed by task E03/F03/US01/T02
(renewal-action, feature-03-renewal-dashboard): an owner/status/action —
`POST /api/renewals/{id}/action`, spec Appendix A, see the HTTP surface
table above — and `Contigo.Renewals`'s first `DbContext`
(`RenewalsDbContext`), which backs that endpoint's
`Contigo.Renewals.Domain.RenewalAction` row. That `DbContext` does not,
though, give `RenewalOpportunity` itself a persisted identity: spec §9.1's
"create/update renewal opportunity" upsert semantics land on the separate
`RenewalAction` (owner/status/action) row, keyed by `ContractId` alone, not
on a stored "renewal" entity — see `RenewalAction`'s own doc comment.
`RenewalOpportunity` remains an in-memory value, not a stored row.
## Renewal Intelligence — explainable priority score

`Contigo.Renewals.Application.PriorityScoreCalculator` (task E03/F01/US02/T01,
us-02-priority-score) is product spec §9.2's formula made concrete: `"Priority
Score = Spend Weight + Time Urgency + Benchmark Opportunity + Price Increase
Risk + Contract Risk"`. Same determinism convention as `RenewalEngine` (pure,
synchronous, no database/HTTP/LLM call) — `Calculate` takes one
`RenewalCalculationResult` (so "days until renewal" is always
`RenewalEngine`'s own arithmetic, never a second copy of it) plus one
`RenewalPriorityInputs` (the raw spend/uplift/contract-risk/benchmark-position
facts `RenewalEngine` does not compute) and returns a `PriorityScoreResult`:
a `TotalScore` (0–100 under the spec-default weights) plus each of the five
components as its own named, explained `PriorityScoreComponent` — spec
§9.2's "Store both total score and component scores so the recommendation is
explainable and tunable" (AC-2), not a single opaque number.

A component whose raw input is unknown never fabricates a guess (Appendix C
rule 10): every component except benchmark opportunity defaults to the
minimum (0); benchmark opportunity defaults to the documented neutral
midpoint (`PriorityScoreCalculator.NeutralComponentScore`, 10 under the spec
default) specifically, because parent story AC-3 names that exact rule —
`"Benchmark-opportunity component reads the R3 benchmark only when available
(else neutral)"`. Today that is *always* the neutral case:
`Contigo.Benchmark.IBenchmarkService` now defines the normalized
`GetBenchmarkAsync` contract (task E04/F01/US01/T01), and
`Contigo.Benchmark.Fixtures.FixtureBenchmarkAdapter` is now registered
behind it via `AddBenchmarkModule` (task E04/F01/US02/T01, see "Benchmark
Service" below), but nothing wires
`RenewalPriorityInputs.BenchmarkMarketPositionPercent` to a real
`GetBenchmarkAsync` call yet — the same "caller supplies it however it likes
today, a real mapping lands later" gap `ContractRenewalTerms` already
documents for this module. Every tier boundary (spend, uplift %,
benchmark %) and the time-urgency tiers (aligned to spec §9.1's own
365/270/180/120/90/60/30-day windows) are fixed, product-spec-cited defaults
— task-02 deliberately did not re-derive that tiering (see next paragraph
for what it did make tunable).

**Task E03/F01/US02/T02 (priority-explainability)** closed both gaps the
paragraph above used to name. *Tunable*: each of the five components' own
*maximum* contribution is now
`Contigo.Renewals.Configuration.PriorityScoreWeightsOptions` (config section
`Renewals:PriorityWeights`, `SpendWeightMax`/`TimeUrgencyMax`/
`BenchmarkOpportunityMax`/`PriceIncreaseRiskMax`/`ContractRiskMax`, each
defaulting to 20 — the untouched spec default) — `PriorityScoreCalculator` rescales every tier's
fixed contribution proportionally (the tier's fraction of the spec-default
20, times the configured maximum), so the tiering itself is unchanged but
each term's weight in the sum is an operator decision, not a compile-time
literal. *Explainable, queryable*: `Contigo.Api.RenewalsEndpointExtensions`
now maps `GET /api/renewals/{contractId}/priority` (see the HTTP surface
table above) — `PriorityScoreCalculator`'s first real host caller, composing
`Contract360QueryService`'s tenant-scoped contract lookup (annual spend, end
date, auto-renewal, risk) the same way `GET /api/renewals` composes
`PortfolioQueryService`; `AnnualUpliftPercent`/`BenchmarkMarketPositionPercent`
stay honestly `null` for the same reason `GET /api/renewals`'s own insight
card does (neither has a real producer yet).

`AddRenewalsModule` registers `PriorityScoreWeightsOptions` the same
"bind lazily from `IConfiguration`, property initializers supply the spec
default" way as `ThresholdWindowOptions` (see below), then registers
`PriorityScoreCalculator` as before — its one constructor parameter is now
that options singleton, injected automatically.

### Renewal threshold scheduler

`Contigo.Renewals.Application.RenewalThresholdScheduler` (task
E03/F02/US01/T01, us-01-threshold-scheduler AC-1/AC-2) is product spec
§9.1's "daily scheduler ... emit threshold events if applicable" made
concrete: it runs `RenewalEngine.CalculateMany` over a tenant's
`ContractRenewalTerms`, then checks each result's `DaysUntilRenewal`/
`DaysUntilCancellationDeadline` against `Contigo.Renewals.Configuration
.ThresholdWindowOptions.DaysBeforeDeadline` (config section
`Renewals:Thresholds`, default 365/270/180/120/90/60/30 days — AC-1,
"configurable"). An exact day-count match raises a `RenewalApproachingEvent`
(`RenewalMilestoneKind.RenewalDate` or `.CancellationDeadline` — a contract
can raise one, both, or neither on a given run) and writes it through
`IAuditWriter` as one `renewal.approaching` entry (spec Appendix B; same
"an audit entry is this codebase's actual event mechanism" convention as
`document.uploaded`/`contract.corrected` — no in-process mediator exists
yet, and picking one is council-owned, not this task's call) — durable and
queryable via `GET /api/audit` even before a real consumer exists.

`Contigo.Worker.Scheduling.RenewalThresholdSchedulerHostedService` is this
module's first real host caller: `WorkerServiceCollectionExtensions
.AddWorkerHost` now calls `AddRenewalsModule` (closing gap 1 that used to
be listed above) and registers this `BackgroundService`, which ticks every
`Worker:RenewalThresholdScheduler:Interval` (default 24h) and, per tenant
batch, calls `RenewalThresholdScheduler.EvaluateThresholdsAsync` from a
fresh DI scope (it must be Scoped, not injected directly into the Singleton
hosted service — it depends on the Scoped `IAuditWriter`). Honest gap: its
`IActiveRenewalContractsSource` port has no real implementation yet — the
default `NoActiveRenewalContractsSource` always returns zero tenants.
Enumerating every tenant's active contracts needs a cross-tenant workspace
listing (`Contigo.Identity.Workspace`, not referenced by `Contigo.Worker`
today) plus a per-tenant RLS-scoped contract query
(`Contigo.Documents.Contracts`) — wiring a real adapter is follow-up
composition work, the same category of gap this section's remaining item
above describes. The timer loop itself is real and proven end to end
(`Contigo.Worker.Tests.RenewalThresholdSchedulerHostedServiceTests`); AC-3
("Scheduler recomputes when a contract/term is corrected") is parent story
task-02's scope ("Alert creation + re-compute on correction"), not this
task's.

**Task E03/F04/US01/T01 (r2-integration) fix:** `RenewalThresholdScheduler
.EvaluateThresholdsAsync` wrote its `renewal.approaching` audit entry
without ever opening an `ITenantContext` scope, so
`TenantRlsConnectionInterceptor` left `app.tenant_id` unset and the Audit
module's own `AddTenantRowLevelSecurity` `WITH CHECK` policy rejected the
insert outright — a real threshold crossing would throw instead of being
recorded. Neither `RenewalThresholdSchedulerTests` (a `RecordingAuditWriter`,
no database) nor `RenewalThresholdSchedulerHostedServiceTests` (a
syntactically-valid-but-never-dialled connection string, by design) ever
exercised a real RLS-enforced connection on this path, so this went
undetected until r2-integration's own real-Postgres proof
(`Contigo.IntegrationTests.R2EndToEndTests`) surfaced it. The method now
opens its own scope before writing, the same convention
`RenewalActionService.SetActionAsync` already follows.

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

## R2 demo smoke test

The automated proof of task E03/F04/US01/T01 (r2-integration) is
`dotnet test` — `Contigo.IntegrationTests.R2EndToEndTests` (AC-1/AC-2: every
active contract gets a deterministic renewal date/cancellation deadline
where data exists, an explainable component-scored priority via `GET
/api/renewals/{id}/priority`, a `renewal.approaching` threshold event that
is durably recorded — never fabricated for a contract with an unknown end
date — and a `POST /api/renewals/{id}/action` upsert) and
`R2CrossTenantIsolationTests` (AC-3, across the whole `GET
/api/renewals` / `GET /api/renewals/{id}/priority` / `POST
/api/renewals/{id}/action` surface). Contracts are seeded directly against
the real, RLS-enforced `DocumentsContractsDbContext` (see
`R2IntegrationFixture.SeedContractAsync`) rather than through the R1 upload
path — R2's own leaf artifacts all take already-validated contract data as
an input, never produce it.

**Honest scope note:** this task's own wave-spec `depends_on` names
`renewal-alerts` (task E03/F02/US01/T02, "Alert creation + re-compute on
correction"), but that task has not landed any code as of this task's own
run. The only "alert" artifact that actually exists is the
`renewal.approaching` **threshold event** (task E03/F02/US01/T01,
`threshold-scheduler`, see above) — a durable, queryable audit entry, not a
persisted, de-duplicated `RenewalAlert` row with recompute-on-correction.
`R2EndToEndTests` proves exactly the former (parent story AC-2's own literal
wording, "Threshold events fire") and no more; a persisted alert entity
with recompute-on-correction remains task E03/F02/US01/T02's own, still-open
file scope.

## Savings Intelligence — deterministic price normalization

`Contigo.Savings.Application.PriceNormalizationCalculator` (task E04/F02/US01/T01,
us-01-price-normalization, the wave-spec's `savings-normalization` artifact) is product spec
§4.3/§10's "Normalize current unit price and compare with benchmark P25/P50/P75... Calculate
current percentile, recommended target and savings range" made concrete: pure, synchronous
arithmetic over a `PriceComparisonRequest` (an already-fetched `Contigo.Benchmark.Contracts
.BenchmarkResult` plus the current total cost) — no database, no HTTP call, no LLM call (Appendix C
rule 6) — returning a `PriceComparisonResult` with a four-way `PriceComparisonStatus`:

- `Compared` — the benchmark had a well-ordered distribution and currencies matched: normalized
  unit price, percentile rank (0-100, linearly interpolated between P25/P50/P75 and clamped at the
  ends — never extrapolated beyond the last known marker), a recommended target range
  (`[min(P25, price), min(P50, price)]` — never above the current price) and a per-unit + total
  savings range are all populated.
- `InvalidQuantity` — `BenchmarkQuery.Quantity` is zero or negative: nothing is computed, not even
  the normalized unit price (division would be meaningless).
- `CurrencyMismatch` — `BenchmarkQuery.Currency` does not equal `BenchmarkResult.Currency`; this
  codebase has no exchange-rate service, so converting would fabricate a rate it does not actually
  know (Appendix C rule 10) — the normalized unit price is still reported in its own currency, but
  no comparison is attempted.
- `InsufficientBenchmarkData` — either `BenchmarkResult.Distribution` is null (ADR-001's explicit
  "insufficient market data" outcome) or it is present but not well-ordered (`P25 <= P50 <= P75`
  does not hold, a data-quality problem this calculator refuses to silently paper over rather than
  fail on).

`PriceComparisonRequest` deliberately reuses `Contigo.Benchmark.Contracts.BenchmarkQuery` (rather
than re-declaring supplier/quantity/term/currency on a second type) for the exact query a caller
already built to fetch the `BenchmarkResult` in the first place — so currency/quantity are
guaranteed to be the values the benchmark lookup itself used, and term alignment (comparing a
12-month contract against 12-month comparables, not 36-month ones) stays the Benchmark Service's
own matching responsibility (product spec §10.4), never re-derived here. `PriceComparisonResult`
echoes the original `BenchmarkResult` unchanged on every outcome, so `Confidence`/`Source`/
`ComparisonDimensions`/`SampleSize`/`UpdatedAt` are always reachable from one result without this
task re-declaring or guessing at task-02's (confidence + provenance propagation) own output shape.

Same "benchmark data only ever arrives as an already-known value, never a live call" convention
`Contigo.Renewals.Application.PriorityScoreCalculator` already established: this calculator's
public API structurally cannot accept a live `Contigo.Benchmark.IBenchmarkService`, so Appendix C
rule 3 ("never call a benchmark provider directly from renewal, savings or quote business logic")
can never become an accidental provider call from this module — proven by
`Contigo.Savings.Tests.PriceNormalizationCalculatorTests.Calculator_never_depends_on_the_live_Benchmark_Service_interface`.

`Contigo.Savings.Application.SavingsProvenanceClassifier` (task E04/F02/US01/T02, us-01-price-
normalization task-02, the wave-spec's `savings-provenance` artifact) closes AC-3 ("Show confidence
+ provenance on the comparison"): `PriceComparisonResult.Provenance` is a computed property — not a
constructor argument, so task-01's own tested shape is unchanged — that derives a
`Contigo.Savings.Application.SavingsProvenance` view from `PriceComparisonResult.Benchmark` on every
access. It carries a `Contigo.Savings.Domain.SavingsConfidenceLevel` (`Low`/`Medium`/`High`, spec's
own UI vocabulary for "Benchmark confidence") alongside the raw `[0, 1]` confidence score,
source/comparison-dimensions/sample-size/updated-at (all echoed unchanged from `BenchmarkResult`),
and a deterministic one-line `Summary`. `Classify`'s thresholds (`HighConfidenceThreshold` = 0.7,
`MediumConfidenceThreshold` = 0.4) are this classifier's own documented, adjustable heuristic — not
a council-locked figure — chosen so `FixtureBenchmarkAdapter`'s own catalog already spans all three
tiers (full-sample matches are High; Zoom's thinner 30-of-50 sample is Medium; Snowflake's 18-of-50
sample, and any supplier+product-only weak match, are Low). `Provenance` is available regardless of
`PriceComparisonResult.Status` — `BenchmarkResult`'s own provenance fields are always populated, so
a caller can show confidence/provenance even for an insufficient-data or currency-mismatch result,
never just the `Compared` case.

Deliberately out of this task's file scope, each a later task's own: a persisted, trackable
`SavingsOpportunity` with status/owner/realized outcome (us-02-savings-opportunity), and any
host/worker wiring that calls `PriceNormalizationCalculator` against real contracts — the same
"wiring lands with the first real caller" sequencing this README's other modules already follow
(see "Renewal Intelligence" above). `AddSavingsModule`/DI registration does not exist yet for the
same reason: nothing calls this calculator from a host yet.

**Incidental fix, task E04/F02/US01/T02:** `backend/tests/Contigo.Benchmark.Tests/ServiceCollectionExtensionsTests.cs`
failed to compile (a prior merge had spliced one test method's closing brace together with a second,
differently-named test's signature line, discarding that second method's body) — fixed to restore
`dotnet build Contigo.slnx`, since a broken build blocks every task, not just this one. The
recovered test body is verified against this module's own current source, not guessed; the
unrecoverable second test is not reinvented. That repair surfaced a separate, still-open
`Contigo.Benchmark` wiring gap, left exactly as found (not this task's module or file scope):
`AddBenchmarkModule` registers `FixtureBenchmarkAdapter` directly as `IBenchmarkService` via
`TryAddSingleton`, but the preceding `TryAddSingleton<IBenchmarkService, BenchmarkAdapterRegistry>`
call already claims that slot (first registration wins), and `FixtureBenchmarkAdapter` does not
implement `IBenchmarkProviderAdapter`, so `BenchmarkAdapterRegistry` can never reach it either — a
container built from `AddBenchmarkModule()` resolves `IBenchmarkService` to an always-adapter-less
`BenchmarkAdapterRegistry` today, not `FixtureBenchmarkAdapter`, contradicting
`Resolved_service_fails_honestly_when_no_adapter_is_registered_yet`'s own
`Assert.IsType<FixtureBenchmarkAdapter>` (that test now fails, honestly, instead of the file
silently not compiling). Fixing the wiring itself is us-02-fixture-adapter's/the adapter-registry
task's own module to redesign, not a Savings-module task's file scope.
Deliberately out of this task's file scope, each a later task's own: confidence + provenance
propagation into whatever surface displays this result (us-01-price-normalization task-02), and any
host/worker wiring that calls `PriceNormalizationCalculator` against real contracts — the same
"wiring lands with the first real caller" sequencing this README's other modules already follow (see
"Renewal Intelligence" above). This calculator itself still has no DI registration for the same
reason: nothing calls it from a host yet — see the next section for what `AddSavingsModule` *does*
now register.

## Savings Intelligence — trackable SavingsOpportunity

`Contigo.Savings.Domain.SavingsOpportunity` (task E04/F02/US02/T01, savings-opportunity, the
wave-spec's `savings-opportunity` artifact; parent story us-02-savings-opportunity AC-2) is this
module's first persisted entity — product spec §6's core data model row "SavingsOpportunity |
supplier, contract/quote, type, current_spend, estimated savings range, confidence, status, owner"
(module-map.md: "Savings | SavingsOpportunity, RealizedSavings | `/api/savings`") made concrete:
`SupplierId`/`ContractId` are cross-module references by id only, deliberately no foreign key (same
treatment `Contigo.Renewals.Domain.RenewalAction.ContractId` already gives its own cross-module
reference — ADR-002 forbids this module from referencing `Contigo.Suppliers.Products` or
`Contigo.Documents.Contracts` at all); `Type` is free text (no ADR/spec fixes a vocabulary);
`CurrentSpend`/`EstimatedSavingsLow`/`EstimatedSavingsHigh` carry an explicit `Currency` (this
codebase has no currency-conversion service anywhere); `Confidence` echoes
`Contigo.Benchmark.Contracts.BenchmarkResult.Confidence` (spec §4.3 "Show benchmark confidence and
provenance"). `Status` (`Identified` / `InProgress` / `Realized`) is read directly off spec §4.3's
own three dashboard KPI buckets ("savings identified" / "savings in progress" / "savings realized")
— see `SavingsOpportunityStatus`'s own doc comment for why no fourth "rejected/dismissed" state
exists yet.

`Contigo.Savings.Application.SavingsOpportunityService` backs `GET /api/savings` (list, newest
identified first) and `PATCH /api/savings/{id}` (a genuine partial update of `owner`/`status`, either
or both — see the HTTP surface table above), tenant-scoped via `ITenantContext.BeginScope` the same
way `Contigo.Renewals.Application.RenewalActionService` is, and writes one `IAuditWriter` entry per
successful mutation (`savings_opportunity.identified` / `savings_opportunity.updated` — spec §14.1).
Also exposes `CreateAsync` ("identify"), proven by `Contigo.Savings.Tests
.SavingsOpportunityServiceTests` but not yet wired to an HTTP route — this task's own AC-1 names only
`GET`/`PATCH`, and nothing in this codebase yet maps a real `PriceComparisonResult` against a real
contract into a `CreateSavingsOpportunityRequest`; that composition (in `Contigo.Api`, "the one
project allowed to reference every module") is a follow-up, the same "wiring lands with the first
real caller" gap the previous section names for `PriceNormalizationCalculator` itself.

`AddSavingsModule` (task E04/F02/US02/T01) gives this module its first `DbContext`
(`SavingsDbContext`) and is now called by `Contigo.Api`'s `Program.cs` — RLS is wired the same
`AddTenantRowLevelSecurity` migration + `TenantRlsConnectionInterceptor` mechanism every other
tenant-scoped module uses (ADR-009), proven by `Contigo.Savings.Tests
.SavingsOpportunityRlsMigrationCheckTests`/`SavingsOpportunityRlsCrossTenantIsolationTests`.
`Contigo.Worker` is not wired to this module yet (no worker job creates opportunities today) — the
same "wiring lands with the first real caller" gap, not attempted by this task.

**Task E04/F02/US02/T02 (realized-savings)** closes the gap the paragraph above used to name:
`Contigo.Savings.Domain.RealizedSavings` (module-map.md's own second named entity for this module,
"Record realized value + audit event", parent story AC-3) is this module's second tenant-scoped
table — one append-only row per captured realized value (never a destructive overwrite, the same
"never destructively overwrite" spirit `Contigo.Documents.Contracts.Domain.ContractVersion`/
`CorrectionHistory` already apply to their own history), in the opportunity's own `Currency` (no
per-row currency — this codebase has no currency-conversion service anywhere). `PATCH
/api/savings/{id}`'s `realizedAmount` field (see the HTTP surface table above) is the only writer,
via `SavingsOpportunityService.UpdateAsync`: a non-negative `realizedAmount` always finalizes
`Status` as `Realized` (either because the caller's own explicit `status` already said so, or
automatically when `status` was omitted — the two facts are not independent, see
`SavingsOpportunityStatus.Realized`'s own doc comment) and inserts one new `RealizedSavings` row,
still exactly one `IAuditWriter` entry per call (`savings_opportunity.realized` takes the place of
`savings_opportunity.updated` for that call, never both). RLS is wired the same
`AddRealizedSavingsRowLevelSecurity` migration + `TenantRlsConnectionInterceptor` mechanism as
every other tenant-scoped table (ADR-009) — proven by `Contigo.Savings.Tests
.SavingsOpportunityRlsMigrationCheckTests` (dynamic per-table discovery, no test change needed) and
the new `RealizedSavingsRlsCrossTenantIsolationTests`. Honest gap, deliberately out of this task's
own file scope: `GET /api/savings`'s list response does not surface any opportunity's realized-value
history — only the `PATCH` response that just recorded one does (see
`SavingsOpportunityResult.RealizedAmount`'s own doc comment) — a rolled-up read (e.g. for the
dashboard's own "savings realized" KPI, spec §4.3) is a follow-up, the same "wiring lands with the
first real caller" gap this section's other paragraphs already document.

## Savings Intelligence — procurement homepage KPIs

Task E04/F03/US01/T01 (savings-kpis, the wave-spec's `savings-kpis` artifact; parent story
us-01-savings-kpis AC-1) adds `GET /api/savings/kpis` — see the HTTP surface table above for the
response shape. Two new pure calculators do the actual arithmetic, each unit-tested independently
of any database (same convention `Contigo.Renewals.Application.RenewalPipelineBuilder`/
`PriorityScoreCalculator` already establish):

- `Contigo.Savings.Application.SavingsKpiCalculator` groups every tenant-scoped
  `SavingsOpportunity` by `Status` then `Currency` for the "Savings Identified"/"Savings In
  Progress"/"Savings Realized" thirds (`SavingsKpiQueryService` is its thin EF-backed fetch half).
- `Contigo.Documents.Contracts.Application.PortfolioAnalysisCalculator` computes "Contracts
  Analyzed"/"Annual Spend Analyzed" from every tenant-scoped `Contract`, flagged by whether any
  linked `Document` reached `DocumentProcessingStatus.Completed` — a `Contract` row alone is not
  "analyzed" (`StagedExtractionService.EnsureContractAsync` creates one as a bootstrap shell before
  extraction even starts) — see that calculator's own doc comment.
  (`PortfolioQueryService.GetAnalysisSummaryAsync` is its fetch half.)

Every money value in the response is grouped by currency, never summed across currencies — the
same "no exchange-rate service anywhere in this codebase" reasoning
`Contigo.Savings.Domain.SavingsOpportunity.Currency`'s own doc comment already gives. "Upcoming
Renewals" adds no dependency on `Contigo.Renewals` at all: `Contigo.Api.SavingsKpiEndpointExtensions`
reuses the exact same auto-renewing-contract query `GET /api/renewals` already runs for its own
`totalCount`, so the homepage KPI and the renewal pipeline list can never silently disagree.

Honest gap, deliberately out of this task's own file scope: `savingsRealized` is computed from each
`SavingsOpportunity`'s own `EstimatedSavingsLow`/`EstimatedSavingsHigh` range, not the separate,
audit-tracked `RealizedSavings` entity — this task's wave-spec dependency is `savings-opportunity`
only (`RealizedSavings` is task E04/F02/US02/T02's own deliverable, scheduled the same wave-spec
phase, so it is not a dependency this task can assume has landed).

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
`DocumentsContracts`, `Audit`, `Renewals`, `Savings`, `Storage`) — never
committed.

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
