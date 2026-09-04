using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Contigo.Documents.Contracts.Application;
using Contigo.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Contigo.IntegrationTests;

/// <summary>
/// Proves the Definition of Done for task E02/F04/US02/T01 (us-02-rag-citations) and its parent
/// story us-02-rag-citations: "`dotnet test` proves citations present and cross-tenant retrieval
/// blocked" — end to end, through the real `POST /api/chat/query` endpoint
/// (<c>Contigo.Api.ChatEndpointExtensions</c>), against a real Postgres+pgvector+RLS Testcontainer
/// (<see cref="R0IntegrationFixture"/>) — the same "one real host, no hand-rolled container" shape
/// <see cref="R0CrossTenantIsolationTests"/> already uses for the R0 path.
///
/// <b>AC-1</b> (auth-before-retrieval) / <b>AC-3</b> (unauthorized documents never enter the LLM
/// context): tenant B's indexed content is never returned for tenant A's query. This holds even
/// though <c>Contigo.AiGateway.Fixtures.FixtureAiGateway</c>'s embeddings are SHA-256 pseudo-vectors
/// with no real semantic ordering (see that type's own doc comment) — tenant isolation comes from
/// <see cref="EmbeddingRetrievalService.SearchAsync"/>'s own <c>tenant_id</c> predicate plus the
/// `embedding` table's RLS policy, never from vector quality, so the proof does not depend on the
/// fixture gateway becoming smarter later.
///
/// <b>AC-2</b> (citations, or an explicit cannot-determine): a tenant with indexed content gets
/// citations pointing only at its own seeded evidence; a tenant with nothing indexed gets an honest
/// "cannot determine" (spec §8.4 "no evidence, no claim") rather than a fabricated answer.
/// </summary>
public sealed class AskContigoRagCrossTenantIsolationTests : IClassFixture<R0IntegrationFixture>
{
    private readonly R0IntegrationFixture _fixture;

    public AskContigoRagCrossTenantIsolationTests(R0IntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Each_tenant_only_ever_sees_its_own_citations()
    {
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();
        var tenantADocumentId = EntityId.New();
        var tenantBDocumentId = EntityId.New();

        await IndexChunkAsync(
            tenantA, "Document", tenantADocumentId,
            "Tenant A's liability cap is $1,000,000 under the AWS master services agreement.");
        await IndexChunkAsync(
            tenantB, "Document", tenantBDocumentId,
            "Tenant B's liability cap is $2,000,000 under its own AWS master services agreement.");

        var client = _fixture.CreateClient();

        var tenantABody = await ParseAsync(
            await PostChatQueryAsync(client, tenantA, "What liability do we have with AWS?"));
        Assert.Equal("Semantic", tenantABody.GetProperty("intent").GetString());
        Assert.True(tenantABody.GetProperty("canDetermine").GetBoolean());

        var tenantACitations = tenantABody.GetProperty("citations").EnumerateArray().ToList();
        Assert.NotEmpty(tenantACitations);
        Assert.All(
            tenantACitations,
            citation => Assert.StartsWith(
                "Document:" + tenantADocumentId, citation.GetProperty("documentId").GetString()));
        Assert.DoesNotContain(
            tenantACitations,
            citation => citation.GetProperty("documentId").GetString()!.Contains(tenantBDocumentId.ToString()));

        // Other direction — proves the isolation above is real, not a coincidence of seed order.
        var tenantBBody = await ParseAsync(
            await PostChatQueryAsync(client, tenantB, "What liability do we have with AWS?"));
        var tenantBCitations = tenantBBody.GetProperty("citations").EnumerateArray().ToList();
        Assert.NotEmpty(tenantBCitations);
        Assert.All(
            tenantBCitations,
            citation => Assert.StartsWith(
                "Document:" + tenantBDocumentId, citation.GetProperty("documentId").GetString()));
        Assert.DoesNotContain(
            tenantBCitations,
            citation => citation.GetProperty("documentId").GetString()!.Contains(tenantADocumentId.ToString()));
    }

    [Fact]
    public async Task A_tenant_with_no_indexed_content_gets_an_honest_cannot_determine_not_a_fabricated_answer()
    {
        var emptyTenant = TenantId.New();
        var client = _fixture.CreateClient();

        var response = await PostChatQueryAsync(client, emptyTenant, "What liability do we have with AWS?");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ParseAsync(response);

        Assert.Equal("Semantic", body.GetProperty("intent").GetString());
        Assert.False(body.GetProperty("canDetermine").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("answer").ValueKind);
        Assert.Equal(0, body.GetProperty("citations").GetArrayLength());
    }

    /// <summary>
    /// Seeds one embedded chunk directly through the real <see cref="EmbeddingRetrievalService"/>
    /// (resolved from the host's own DI container, not a hand-rolled one) rather than over HTTP:
    /// indexing is not an HTTP-exposed capability in this wave (see <c>backend/README.md</c>'s "Ask
    /// Contigo" section) — this test proves the read/answer side
    /// (<c>POST /api/chat/query</c>), not the write/indexing side, which
    /// <c>Contigo.Documents.Contracts.Tests.EmbeddingRetrievalServiceTests</c> already covers on its
    /// own.
    /// </summary>
    private async Task IndexChunkAsync(TenantId tenantId, string sourceType, EntityId sourceId, string chunkText)
    {
        using var scope = _fixture.Services.CreateScope();
        var embeddingRetrievalService = scope.ServiceProvider.GetRequiredService<EmbeddingRetrievalService>();

        var result = await embeddingRetrievalService.IndexChunkAsync(tenantId, sourceType, sourceId, 0, chunkText);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : string.Empty);
    }

    private static async Task<HttpResponseMessage> PostChatQueryAsync(HttpClient client, TenantId tenantId, string question)
    {
        // Must await inside this `using` block (not return the un-awaited Task): disposing
        // `request` before SendAsync's work actually completes would race its Content stream.
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat/query")
        {
            Content = JsonContent.Create(new { question }),
        };
        request.Headers.Add("X-Tenant-Id", tenantId.Value.ToString());
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> ParseAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
