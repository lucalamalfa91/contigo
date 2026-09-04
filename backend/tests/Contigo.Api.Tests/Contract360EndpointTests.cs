using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Contigo.Api.Tests;

/// <summary>
/// Host-level proof for task E02/F03/US02/T01 (us-02-contract-360-aggregate) that
/// `GET /api/contracts/{id}` is actually mapped in <c>Program.cs</c> (via
/// <c>ContractsEndpointExtensions</c>) and enforces its request-shape guard clauses — mirrors
/// <see cref="PortfolioEndpointTests"/>/<see cref="ContractCorrectionEndpointTests"/>'s own "not
/// just a placeholder" purpose. Only exercises branches that return before any database call is
/// made (the tenant-header check and the route-id parse both run before
/// <c>Contract360QueryService</c> is ever called), so — like those two — this needs no running
/// Postgres. The success path (real rows, real tab assembly, real tenant scoping) is proven by
/// <c>Contigo.Documents.Contracts.Tests.Contract360QueryServiceTests</c> instead, against a real
/// Postgres+RLS Testcontainer.
/// </summary>
public sealed class Contract360EndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public Contract360EndpointTests(WebApplicationFactory<Program> factory)
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

        var response = await client.GetAsync($"/api/contracts/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_tenant_header_returns_400()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/contracts/{Guid.NewGuid()}");
        request.Headers.Add("X-Tenant-Id", "not-a-guid");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_contract_id_returns_400()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/contracts/not-a-guid");
        request.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
