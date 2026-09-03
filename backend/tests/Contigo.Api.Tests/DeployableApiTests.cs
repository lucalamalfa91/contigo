using System.Net;
using Contigo.Documents.Contracts.Infrastructure;
using Contigo.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Contigo.Api.Tests;

/// <summary>
/// Proves the Definition of Done for task E01/F04/US04/T01 (deployable-api, ADR-002): the API
/// host actually boots as a composition root and serves `/health`, and has really wired the
/// Documents/Contracts module into its DI container — not just left the "module registration
/// will go here" placeholder from the solution scaffold (E01/F04/US01/T01).
/// </summary>
public sealed class DeployableApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DeployableApiTests(WebApplicationFactory<Program> factory)
    {
        // A syntactically valid Npgsql connection string satisfies Program.cs's startup check
        // and UseNpgsql()'s eager parsing. Nothing below opens a real connection, so no running
        // Postgres instance is required for this test.
        _factory = factory.WithWebHostBuilder(builder => builder.UseSetting(
            "ConnectionStrings:DocumentsContracts",
            "Host=localhost;Port=5432;Database=contigo_dev;Username=contigo;Password=contigo;Include Error Detail=true"));
    }

    [Fact]
    public async Task Health_endpoint_returns_200_ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Host_composes_the_documents_contracts_module_into_di()
    {
        using var scope = _factory.Services.CreateScope();

        // AC-1 ("API host composes modules via DI"): resolve the module's own DbContext and the
        // shared tenant claim it depends on straight out of the host's real service provider —
        // the same one Program.cs builds, not a hand-rolled container.
        var dbContext = scope.ServiceProvider.GetRequiredService<DocumentsContractsDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        Assert.NotNull(dbContext);
        Assert.NotNull(tenantContext);
    }
}
