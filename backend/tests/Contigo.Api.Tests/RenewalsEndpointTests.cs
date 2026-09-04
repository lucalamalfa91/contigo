using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Contigo.Api.Tests;

/// <summary>
/// Host-level proof for task E03/F03/US01/T01 (us-01-renewal-dashboard-api) that `GET /api/renewals`
/// is actually mapped in <c>Program.cs</c> and enforces its request-shape guard clause, and for task
/// E03/F01/US02/T02 (priority-explainability) that `GET /api/renewals/{contractId}/priority` does
/// the same — mirrors <see cref="PortfolioEndpointTests"/>'s own "not just a placeholder" purpose.
/// Only exercises branches that return before any database call is made (the tenant-header check,
/// and for the priority route the route-id parse, both run before
/// <c>Contigo.Documents.Contracts.Application.Contract360QueryService</c>/<c>PortfolioQueryService</c>
/// are ever called), so — like <see cref="PortfolioEndpointTests"/>/<see cref="Contract360EndpointTests"/>
/// — this needs no running Postgres. The success path (real rows, real pipeline/insight-card
/// construction, real priority-score composition) is proven at the plain-unit-test level instead —
/// <c>Contigo.Renewals.Tests.RenewalPipelineBuilderTests</c> for the dashboard,
/// <c>Contigo.Renewals.Tests.PriorityScoreCalculatorTests</c> for the priority score itself — per
/// this task's own "Tests required" level (unit, no database).
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

    // ----- GET /api/renewals/{contractId}/priority (task E03/F01/US02/T02) -----

    [Fact]
    public async Task Priority_missing_tenant_header_returns_400()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/renewals/{Guid.NewGuid()}/priority");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Priority_invalid_tenant_header_returns_400()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/renewals/{Guid.NewGuid()}/priority");
        request.Headers.Add("X-Tenant-Id", "not-a-guid");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Priority_invalid_contract_id_returns_400()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/renewals/not-a-guid/priority");
        request.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
