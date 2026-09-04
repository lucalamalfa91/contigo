using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Contigo.Api.Tests;

/// <summary>
/// Host-level proof for task E02/F04/US02/T01 (us-02-rag-citations) that `POST /api/chat/query` is
/// actually mapped in <c>Program.cs</c> (via <see cref="ChatEndpointExtensions"/>) and enforces its
/// request-shape guard clauses — mirrors <see cref="Contract360EndpointTests"/>/
/// <see cref="PortfolioEndpointTests"/>'s own "not just a placeholder" purpose. Only exercises
/// branches that return before any database call is made: the tenant-header check, the
/// blank-question check, and the <c>Structured</c>-intent "not wired yet" branch all run before
/// <c>Contigo.Documents.Contracts.Application.EmbeddingRetrievalService.SearchAsync</c> is ever
/// called — so, like those two sibling test classes, this needs no running Postgres. The `Semantic`
/// success path (real tenant-scoped retrieval, real citations, real cross-tenant isolation) is
/// proven by <c>Contigo.IntegrationTests.AskContigoRagCrossTenantIsolationTests</c> instead,
/// against a real Postgres+pgvector+RLS Testcontainer.
/// </summary>
public sealed class ChatEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ChatEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(
                "ConnectionStrings:DocumentsContracts",
                "Host=localhost;Port=5432;Database=contigo_dev;Username=contigo;Password=contigo;Include Error Detail=true");
            builder.UseSetting("ConnectionStrings:Storage", "UseDevelopmentStorage=true");
        });
    }

    [Fact]
    public async Task Missing_tenant_header_returns_400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chat/query", new { question = "What liability do we have with AWS?" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_tenant_header_returns_400()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat/query")
        {
            Content = JsonContent.Create(new { question = "What liability do we have with AWS?" }),
        };
        request.Headers.Add("X-Tenant-Id", "not-a-guid");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_or_blank_question_returns_400(string? question)
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat/query")
        {
            Content = JsonContent.Create(new { question }),
        };
        request.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Structured_question_reports_cannot_determine_with_an_honest_message_instead_of_a_database_call()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat/query")
        {
            // Matches AskContigoQueryRouter's StructuredKeywords ("annual spend") — routed
            // Structured, so this must never reach EmbeddingRetrievalService.SearchAsync (which
            // would need a real Postgres this test deliberately does not provide).
            Content = JsonContent.Create(new { question = "What is our annual spend?" }),
        };
        request.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;

        Assert.Equal("Structured", root.GetProperty("intent").GetString());
        Assert.False(root.GetProperty("canDetermine").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("answer").ValueKind);
        Assert.Equal(0, root.GetProperty("citations").GetArrayLength());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("message").GetString()));
    }
}
