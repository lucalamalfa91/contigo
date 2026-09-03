// Contigo API Host — thin composition root (ADR-002).
// Wires all modules via DI; contains no business logic.
using Contigo.Documents.Contracts.Infrastructure;

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

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();

// Exposes the top-level-statement entry point to WebApplicationFactory<Program> in the
// Contigo.Api.Tests integration test project (a separate assembly).
public partial class Program { }
