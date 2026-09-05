using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Contigo.Api.Tests;

/// <summary>
/// Host-level proof for task E05/F01/US01/T01 (quote-extraction; parent story
/// us-01-quote-line-extraction AC-1) that `POST /api/quotes` is actually mapped in
/// <c>Program.cs</c> and enforces its request-shape guard clauses — mirrors
/// <see cref="DocumentUploadEndpointTests"/>'s own purpose and shape exactly. Only exercises
/// branches that return before any database/storage/AI-Gateway call is made, so this needs no
/// running Postgres, Azurite, or Foundry endpoint — <c>appsettings.Development.json</c>'s own
/// syntactically-valid `ConnectionStrings:Quotes` default (loaded automatically in the
/// `WebApplicationFactory`'s "Development" environment, the same way every other required
/// connection string this host now checks at startup already is) is enough to satisfy
/// <c>Program.cs</c>'s fail-fast startup check.
/// </summary>
public sealed class QuotesEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public QuotesEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Missing_tenant_header_returns_400()
    {
        var client = _factory.CreateClient();
        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent([1, 2, 3]), "file", "quote.pdf" },
        };

        var response = await client.PostAsync("/api/quotes", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_tenant_header_returns_400()
    {
        var client = _factory.CreateClient();
        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent([1, 2, 3]), "file", "quote.pdf" },
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/quotes") { Content = content };
        request.Headers.Add("X-Tenant-Id", "not-a-guid");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Missing_file_field_returns_400()
    {
        var client = _factory.CreateClient();
        using var content = new MultipartFormDataContent
        {
            { new StringContent("not-a-file"), "note" },
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/quotes") { Content = content };
        request.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Non_multipart_body_returns_400()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/quotes")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
