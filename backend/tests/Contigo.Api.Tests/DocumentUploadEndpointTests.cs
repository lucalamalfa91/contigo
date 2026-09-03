using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Contigo.Api.Tests;

/// <summary>
/// Host-level proof for task E01/F06/US01/T01 (us-01-document-upload) that `POST /api/documents`
/// is actually mapped in <c>Program.cs</c> and enforces its request-shape guard clauses — mirrors
/// <see cref="DeployableApiTests"/>' own "not just a placeholder" purpose. Only exercises
/// branches that return before any database/storage call is made, so — like
/// <see cref="DeployableApiTests"/> — this needs no running Postgres or Azurite; a syntactically
/// valid connection string is enough to satisfy Program.cs's startup checks.
/// </summary>
public sealed class DocumentUploadEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DocumentUploadEndpointTests(WebApplicationFactory<Program> factory)
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
        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent([1, 2, 3]), "file", "contract.pdf" },
        };

        var response = await client.PostAsync("/api/documents", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_tenant_header_returns_400()
    {
        var client = _factory.CreateClient();
        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent([1, 2, 3]), "file", "contract.pdf" },
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/documents") { Content = content };
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
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/documents") { Content = content };
        request.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
