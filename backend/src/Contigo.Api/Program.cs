// Contigo API Host — thin composition root (ADR-002).
// Wires all modules via DI; contains no business logic.
using Contigo.Api.Infrastructure;
using Contigo.Documents.Contracts.Application;
using Contigo.Documents.Contracts.Infrastructure;
using Contigo.SharedKernel;

var builder = WebApplication.CreateBuilder(args);

// Module registration: each module exposes an AddXxx(IServiceCollection) extension method
// (ADR-002); the host calls it once it takes a dependency on that module. Documents/Contracts
// is the first module with real infrastructure to wire in (us-03's RLS backstop rides along
// automatically via AddDocumentsContractsModule). Further modules register here the same way
// as their own tasks land — this call list is the "composition" ADR-002 asks the host to do.
var documentsContractsConnectionString = builder.Configuration.GetConnectionString("DocumentsContracts")
    ?? throw new InvalidOperationException(
        "Missing required configuration 'ConnectionStrings:DocumentsContracts' " +
        "(set env var ConnectionStrings__DocumentsContracts in deployed environments).");

builder.Services.AddDocumentsContractsModule(documentsContractsConnectionString);

// Object storage (ADR-005 "Object storage" row, ADR-011): the Azure Blob Storage adapter is
// wired here, in the host, and only here — domain modules see IDocumentStorage, never the Azure
// SDK (ADR-002). Container name is fixed by Terraform (infra/modules/storage/main.tf).
var storageConnectionString = builder.Configuration.GetConnectionString("Storage")
    ?? throw new InvalidOperationException(
        "Missing required configuration 'ConnectionStrings:Storage' " +
        "(set env var ConnectionStrings__Storage in deployed environments).");

builder.Services.AddAzureBlobDocumentStorage(storageConnectionString);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

// Task E01/F06/US01/T01 (us-01-document-upload, AC-1): stores the uploaded bytes in
// tenant-scoped blob storage and creates the queued classification job
// (DocumentUploadService owns the actual business logic; this delegate only translates
// HTTP <-> the service call, per ADR-002's "host is a thin composition root").
//
// ADR-010 (Entra ID/OIDC) is not in the "architecture decisions in force" list for this task,
// so there is no validated caller identity/JWT yet. The tenant is taken from an explicit
// X-Tenant-Id header instead of a token claim — a deliberate, recorded interim placeholder.
// Replace with claim-based tenant resolution once the auth-middleware task lands.
app.MapPost("/api/documents", async Task<IResult> (
    HttpRequest request,
    DocumentUploadService uploadService,
    CancellationToken cancellationToken) =>
{
    if (!request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeaderValues)
        || !Guid.TryParse(tenantHeaderValues.ToString(), out var tenantGuid))
    {
        return Results.BadRequest("A valid 'X-Tenant-Id' header (a GUID) is required.");
    }

    if (!request.HasFormContentType)
    {
        return Results.BadRequest("Expected multipart/form-data with a 'file' field.");
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files["file"];
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest("A non-empty 'file' form field is required.");
    }

    await using var content = file.OpenReadStream();
    var result = await uploadService.UploadAsync(
        new TenantId(tenantGuid), file.FileName, file.ContentType, content, cancellationToken);

    if (result.IsFailure)
    {
        return Results.BadRequest(result.Error);
    }

    var uploaded = result.Value;
    return Results.Created($"/api/documents/{uploaded.DocumentId}", new
    {
        id = uploaded.DocumentId.Value,
        fileName = uploaded.FileName,
        mimeType = uploaded.MimeType,
        processingStatus = uploaded.ProcessingStatus.ToString(),
        createdAt = uploaded.CreatedAt,
    });
});

app.Run();

// Exposes the top-level-statement entry point to WebApplicationFactory<Program> in the
// Contigo.Api.Tests integration test project (a separate assembly).
public partial class Program { }
