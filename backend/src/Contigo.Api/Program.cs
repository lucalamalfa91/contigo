// Contigo API Host — thin composition root (ADR-002).
// Wires all modules via DI; contains no business logic.
using Contigo.Api;
using Contigo.Api.Infrastructure;
using Contigo.Audit.Infrastructure;
using Contigo.Documents.Contracts.Application;
using Contigo.Documents.Contracts.Infrastructure;
using Contigo.Identity.Workspace.Infrastructure;
using Contigo.SharedKernel;

var builder = WebApplication.CreateBuilder(args);

// Module registration: each module exposes an AddXxx(IServiceCollection) extension method
// (ADR-002); the host calls it once it takes a dependency on that module. Documents/Contracts
// is the first module with real infrastructure to wire in (us-03's RLS backstop rides along
// automatically via AddDocumentsContractsModule). Further modules register here the same way
// as their own tasks land — this call list is the "composition" ADR-002 asks the host to do.
//
// Task E01/F09/US01/T01 (r0-integration, AC-1 "create workspace"): Identity/Workspace's own
// AddIdentityWorkspaceModule already existed (task E01/F05/US01/T01/T02) but had never been
// called by a host — no endpoint used to attach a tenant claim to. WorkspaceEndpointExtensions
// below is that first endpoint.
var identityWorkspaceConnectionString = builder.Configuration.GetConnectionString("IdentityWorkspace")
    ?? throw new InvalidOperationException(
        "Missing required configuration 'ConnectionStrings:IdentityWorkspace' " +
        "(set env var ConnectionStrings__IdentityWorkspace in deployed environments).");

builder.Services.AddIdentityWorkspaceModule(identityWorkspaceConnectionString);

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

// Audit module (task E01/F06/US02/T02, GET /api/audit). Fails fast with a named error rather
// than silently falling back when the config is missing (same "fail loud, not silent"
// convention this codebase already uses for required CI/CD config).
var auditConnectionString = builder.Configuration.GetConnectionString("Audit")
    ?? throw new InvalidOperationException(
        "Missing required configuration: ConnectionStrings:Audit " +
        "(see appsettings.Development.json for the local dev default).");

builder.Services.AddAuditModule(auditConnectionString);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

// Task E01/F09/US01/T01 (r0-integration, AC-1 "create workspace -> invite"): see
// WorkspaceEndpointExtensions for the endpoints themselves.
app.MapWorkspaceEndpoints();

// Task E01/F06/US01/T01 (us-01-document-upload, AC-1): stores the uploaded bytes in
// tenant-scoped blob storage and creates the queued classification job
// (DocumentUploadService owns the actual business logic; this delegate only translates
// HTTP <-> the service call, per ADR-002's "host is a thin composition root").
//
// ADR-010 (Entra ID/OIDC) is not in the "architecture decisions in force" list for this task,
// so there is no validated caller identity/JWT yet. The tenant is taken from an explicit
// X-Tenant-Id header instead of a token claim — a deliberate interim placeholder (paired with
// DocumentUploadService.UnattributedActor). Deliberately NOT promoted to
// reports/open-questions.md by this task: that file is appended to by every wave/* implementer
// branch in this fan-out, and concurrent appends to it have previously broken a phase-barrier
// merge, so a mid-wave append here would risk repeating that. Fold one consolidated entry for
// both placeholders into the ledger at a safe point (e.g. when the auth-middleware task is
// authored), then replace both with claim-based tenant/actor resolution.
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

// Task E01/F06/US01/T02 (us-01-document-upload, AC-3): reads back the metadata/status AC-2
// already persists (task T01), scoped to the caller's tenant. Same interim X-Tenant-Id
// placeholder as POST /api/documents above (ADR-010 is not in force for this task either, so
// there is still no validated caller principal to take the tenant from) — see that endpoint's
// comment for why this is not promoted to reports/open-questions.md by this task.
app.MapGet("/api/documents/{id}", async Task<IResult> (
    string id,
    HttpRequest request,
    DocumentQueryService queryService,
    CancellationToken cancellationToken) =>
{
    if (!request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeaderValues)
        || !Guid.TryParse(tenantHeaderValues.ToString(), out var tenantGuid))
    {
        return Results.BadRequest("A valid 'X-Tenant-Id' header (a GUID) is required.");
    }

    if (!Guid.TryParse(id, out var documentGuid))
    {
        return Results.BadRequest("The document id in the route must be a GUID.");
    }

    var metadata = await queryService
        .GetByIdAsync(new TenantId(tenantGuid), new EntityId(documentGuid), cancellationToken)
        .ConfigureAwait(false);

    if (metadata is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(new
    {
        id = metadata.DocumentId.Value,
        contractId = metadata.ContractId?.Value,
        fileName = metadata.FileName,
        mimeType = metadata.MimeType,
        documentType = metadata.DocumentType.ToString(),
        processingStatus = metadata.ProcessingStatus.ToString(),
        createdAt = metadata.CreatedAt,
    });
});

// Task E01/F06/US02/T02 (us-02-audit-baseline, AC-2): authorized, tenant-scoped GET /api/audit.
// See AuditEndpointExtensions for the endpoint itself and WorkspacePrincipalAuthorization for
// the authorization decision (401 vs 403 vs the tenant-scoped read).
app.MapAuditEndpoints();

app.Run();

// Exposes the top-level-statement entry point to WebApplicationFactory<Program> in the
// Contigo.Api.Tests integration test project (a separate assembly).
public partial class Program { }
