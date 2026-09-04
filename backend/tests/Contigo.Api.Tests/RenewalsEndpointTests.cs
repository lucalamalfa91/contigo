using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Contigo.Api.Tests;

/// <summary>
/// Host-level proof for task E03/F03/US01/T01 (us-01-renewal-dashboard-api) that `GET /api/renewals`
/// is actually mapped in <c>Program.cs</c> and enforces its request-shape guard clause — mirrors
/// <see cref="PortfolioEndpointTests"/>'s own "not just a placeholder" purpose. Only exercises the
/// branch that returns before any database call is made (the tenant-header check runs before
/// <c>PortfolioQueryService</c> is ever called), so — like <see cref="PortfolioEndpointTests"/> —
/// this needs no running Postgres. The success path (real rows, real pipeline/insight-card
/// construction) is proven by <c>Contigo.Renewals.Tests.RenewalPipelineBuilderTests</c> instead —
/// a plain unit test, no database, per this task's own "Tests required" level.
/// </summary>
public sealed class RenewalsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RenewalsEndpointTests(WebApplicationFactory<Program> factory)
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

        var response = await client.GetAsync("/api/renewals");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_tenant_header_returns_400()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/renewals");
        request.Headers.Add("X-Tenant-Id", "not-a-guid");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
