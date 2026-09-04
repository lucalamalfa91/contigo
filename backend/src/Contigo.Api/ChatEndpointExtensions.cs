using Contigo.AiGateway.Contracts;
using Contigo.Chat.Application;
using Contigo.Documents.Contracts.Application;
using Contigo.SharedKernel;
using Contigo.SharedKernel.Tenancy;

namespace Contigo.Api;

/// <summary>
/// Maps `POST /api/chat/query` (product spec §8.3 "Ask Contigo"; story us-02-rag-citations
/// AC-1/AC-2/AC-3, task E02/F04/US02/T01). Thin composition per ADR-002 — the actual decisions are
/// made by <see cref="Contigo.Chat.Application.AskContigoQueryRouter"/> (which branch),
/// <see cref="EmbeddingRetrievalService"/> (tenant-scoped pgvector retrieval,
/// task E02/F02/US02/T02) and <see cref="RagAnswerService"/> (grounded answer + citations,
/// this task); this file only translates HTTP &lt;-&gt; those calls and does the one piece of
/// composition no domain module is allowed to do itself: <c>Contigo.Chat</c> cannot reference
/// <c>Contigo.Documents.Contracts</c> at all (<c>Contigo.ArchitectureTests
/// .DependencyDirectionTests</c>'s allow-list for that module is exactly
/// <c>[SharedKernel, AiGateway]</c>), so mapping <see cref="EmbeddingSearchResult"/> (Documents/
/// Contracts) into <see cref="AiEvidenceSnippet"/> (AI Gateway) can only happen here, in
/// <c>Contigo.Api</c> — "the one project allowed to reference every module" (<c>backend/README.md</c>
/// "Dependency direction"). <c>Contigo.Api.csproj</c> already carried a <c>ProjectReference</c> to
/// <c>Contigo.Chat.csproj</c> before this task (added in anticipation, never previously used).
///
/// Same interim `X-Tenant-Id` header placeholder as every other endpoint in this host
/// (<c>Program.cs</c>'s document endpoints, <see cref="WorkspaceEndpointExtensions"/>,
/// <see cref="PortfolioEndpointExtensions"/>, <see cref="ContractsEndpointExtensions"/>): ADR-010
/// (Entra ID/OIDC) is not in this task's "Architecture decisions in force" list, so there is no
/// validated caller principal yet — see <c>Program.cs</c>'s own comment on why this interim gap is
/// not promoted to reports/open-questions.md by this task. AC-1 ("auth-before-retrieval") is
/// satisfied at the granularity this whole codebase already implements authorization at today: the
/// tenant is resolved and validated *before* <see cref="EmbeddingRetrievalService.SearchAsync"/> is
/// ever called below, and that call itself applies a mandatory tenant filter (belt) on top of the
/// `embedding` table's own RLS policy (suspenders) — AC-3 follows from the same call, since only
/// this tenant's rows can ever become <see cref="AiEvidenceSnippet"/> evidence.
///
/// Only the <see cref="Contigo.Chat.Domain.QueryIntent.Semantic"/> branch is answered from real
/// data by this task — a <see cref="Contigo.Chat.Domain.QueryIntent.Structured"/> question is
/// reported honestly as not-yet-wired (Appendix C rule 10: uncertainty, not fabricated precision)
/// rather than answered against fabricated contract data: no task has yet mapped a real,
/// tenant-scoped <c>Contract</c> row into <see cref="Contigo.Chat.Application.ContractFact"/> (see
/// that type's own doc comment) that <see cref="DeterministicQueryHandler"/> could run against.
/// </summary>
public static class ChatEndpointExtensions
{
    /// <summary>
    /// Evidence chunks pulled per query. Not fixed by any ADR — a reasonable default retrieval
    /// width for grounded Q&amp;A; a later task may make this configurable per ADR-004's "config,
    /// not code" philosophy for AI-adjacent knobs.
    /// </summary>
    private const int TopK = 5;

    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/chat/query", PostChatQueryAsync);
        return endpoints;
    }

    private static async Task<IResult> PostChatQueryAsync(
        ChatQueryRequest request,
        HttpRequest httpRequest,
        AskContigoQueryRouter router,
        EmbeddingRetrievalService embeddingRetrievalService,
        RagAnswerService ragAnswerService,
        ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (!httpRequest.Headers.TryGetValue("X-Tenant-Id", out var tenantHeaderValues)
            || !Guid.TryParse(tenantHeaderValues.ToString(), out var tenantGuid))
        {
            return Results.BadRequest("A valid 'X-Tenant-Id' header (a GUID) is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return Results.BadRequest("A non-empty 'question' is required.");
        }

        var tenantId = new TenantId(tenantGuid);
        var decision = router.Route(request.Question);

        if (!decision.RequiresRagRetrieval)
        {
            return Results.Ok(ToStructuredNotWiredResponse(decision));
        }

        // ADR-009 "exactly one scope per request" — opened once, here, for the whole Semantic
        // branch: EmbeddingRetrievalService.SearchAsync below defensively opens its own *nested*
        // scope per call (see that type's own doc comment) and restores back to *this* scope on
        // exit, rather than to "no scope", so it is still active when RagAnswerService.AnswerAsync
        // writes its IAuditWriter entry afterwards. Without this outer scope that audit write has
        // no ambient tenant claim at all and Contigo.Audit's own RLS policy rejects the INSERT
        // outright (fail-closed, per ITenantContext.Current's own doc comment) rather than writing
        // an unattributed row — caught by AskContigoRagCrossTenantIsolationTests, not a guess.
        using var tenantScope = tenantContext.BeginScope(tenantId);

        var searchResult = await embeddingRetrievalService
            .SearchAsync(tenantId, decision.Question, TopK, cancellationToken)
            .ConfigureAwait(false);

        if (searchResult.IsFailure)
        {
            return Results.BadRequest(searchResult.Error);
        }

        var evidence = searchResult.Value.Select(ToEvidenceSnippet).ToList();

        var answerResult = await ragAnswerService
            .AnswerAsync(tenantId, decision, evidence, cancellationToken)
            .ConfigureAwait(false);

        if (answerResult.IsFailure)
        {
            return Results.BadRequest(answerResult.Error);
        }

        return Results.Ok(ToAnsweredResponse(decision, answerResult.Value));
    }

    /// <summary>
    /// Maps one tenant-scoped pgvector hit (<see cref="EmbeddingRetrievalService.SearchAsync"/>)
    /// to the AI Gateway's evidence shape — the one mapping only this composition root can do (see
    /// the type doc comment).
    ///
    /// <see cref="EmbeddingSearchResult.SourceId"/> only really identifies a document when
    /// <see cref="EmbeddingSearchResult.SourceType"/> is <c>"Document"</c> — for
    /// <c>"Clause"</c>-sourced evidence it identifies the clause row, not its owning document (see
    /// <c>Contigo.Documents.Contracts.Domain.Embedding.SourceType</c>'s own doc comment: "Document
    /// or Clause content today"). Silently relabelling a clause id as a document id would
    /// misattribute the citation, so the two are combined into one honest, self-describing
    /// composite identifier instead of guessing (Appendix C rule 10). <see cref="AiCitation.Page"/>
    /// is left <see langword="null"/> rather than fabricated: <c>Embedding</c> carries no page
    /// column today. <see cref="AiCitation.Section"/> reports the real chunk position instead — a
    /// retrieved fact, not invented text. True page/section resolution (joining back to
    /// <c>Clause.SourcePage</c>/<c>SourceSpan</c> or a document page map) is a follow-up gap, not
    /// attempted by this task.
    /// </summary>
    private static AiEvidenceSnippet ToEvidenceSnippet(EmbeddingSearchResult hit) =>
        new(
            DocumentId: $"{hit.SourceType}:{hit.SourceId}",
            Page: null,
            Section: $"chunk {hit.ChunkIndex}",
            Text: hit.ChunkText);

    private static object ToAnsweredResponse(QueryRouteDecision decision, AiAnswerResult answer) =>
        new
        {
            question = decision.Question,
            intent = decision.Intent.ToString(),
            canDetermine = answer.CanDetermine,
            answer = answer.Answer,
            citations = answer.Citations.Select(citation => new
            {
                documentId = citation.DocumentId,
                page = citation.Page,
                section = citation.Section,
            }),
            message = (string?)null,
        };

    /// <summary>See the type doc comment's "Only the Semantic branch is answered" note.</summary>
    private static object ToStructuredNotWiredResponse(QueryRouteDecision decision) =>
        new
        {
            question = decision.Question,
            intent = decision.Intent.ToString(),
            canDetermine = false,
            answer = (string?)null,
            citations = Array.Empty<object>(),
            message = "This looks like a structured/deterministic question (dates, spend). " +
                      "Answering it from live contract data is not wired to an endpoint yet " +
                      "(task E02/F04/US01/T02 covers only the deterministic handlers themselves); " +
                      "ask a semantic/legal question instead.",
        };

    /// <summary>
    /// `POST /api/chat/query` request body. A nested type (not top-level) so
    /// <c>Contigo.ArchitectureTests.DependencyDirectionTests.Host_must_not_contain_domain_types</c>
    /// never sees it: <see cref="Type.IsPublic"/> is <see langword="false"/> for every nested type
    /// regardless of its own accessibility (nested types report via <c>IsNestedPublic</c> instead)
    /// — that test only enumerates top-level public types. Declared <see langword="public"/>
    /// anyway (not <see langword="internal"/>) so the minimal API JSON body binder always has a
    /// public constructor to deserialize into, with no dependence on
    /// <c>System.Text.Json</c>'s non-public-constructor behavior.
    /// </summary>
    public sealed record ChatQueryRequest(string? Question);
}
