// Contigo API Host — thin composition root (ADR-002).
// Wires all modules via DI; contains no business logic.
using Contigo.Api;
using Contigo.Audit.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Each module exposes an AddXxx(IServiceCollection) extension method; more are wired here as
// their first host caller lands. Contigo.Audit is first (task E01/F06/US02/T02, GET /api/audit).
// Fails fast with a named error rather than silently falling back when the config is missing
// (same "fail loud, not silent" convention this codebase already uses for required CI/CD config).
var auditConnectionString = builder.Configuration.GetConnectionString("Audit")
    ?? throw new InvalidOperationException(
        "Missing required configuration: ConnectionStrings:Audit " +
        "(see appsettings.Development.json for the local dev default).");
builder.Services.AddAuditModule(auditConnectionString);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapAuditEndpoints();

app.Run();
