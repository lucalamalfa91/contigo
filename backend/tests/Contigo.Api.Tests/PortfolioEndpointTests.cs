using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Contigo.Api.Tests;

/// <summary>
/// Host-level proof for task E02/F03/US01/T01 (us-01-portfolio-list-filters) that
/// `GET /api/contracts` is actually mapped in <c>Program.cs</c> and enforces its request-shape
/// guard clauses — mirrors <see cref="DocumentMetadataEndpointTests"/>'s own "not just a
/// placeholder" purpose. Only exercises branches that return before any database call is made
/// (the tenant-header check and the AC-2 filter parsing, plus task E02/F03/US01/T02's page/pageSize
/// parsing, all run before <c>PortfolioQueryService</c> is ever called), so — like
/// <see cref="DocumentMetadataEndpointTests"/> — this needs no running Postgres. The success path
/// (real rows, real filtering, real tenant scoping, real paging) is proven by
/// <c>Contigo.Documents.Contracts.Tests.PortfolioQueryServiceTests</c> instead, against a real
/// Postgres+RLS Testcontainer.
/// </summary>
public sealed class PortfolioEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PortfolioEndpointTests(WebApplicationFactory<Program> factory)
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

        var response = await client.GetAsync("/api/contracts");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_tenant_header_returns_400()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/contracts");
        request.Headers.Add("X-Tenant-Id", "not-a-guid");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("supplierId=not-a-guid")]
    [InlineData("risk=not-a-severity")]
    [InlineData("autoRenewal=not-a-bool")]
    [InlineData("minAnnualSpend=not-a-number")]
    [InlineData("maxAnnualSpend=not-a-number")]
    [InlineData("renewalFrom=not-a-date")]
    [InlineData("renewalTo=not-a-date")]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("page=not-a-number")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("pageSize=not-a-number")]
    public async Task Malformed_filter_or_page_query_parameter_returns_400(string queryString)
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/contracts?{queryString}");
        request.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
