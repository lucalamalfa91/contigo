// Contigo Worker Host — thin composition root (ADR-002).
// References the same domain/application libraries as the API host (parent story us-04 AC-2:
// "Worker host references the same application services"); hosts background processing
// (extraction, renewal recomputation, benchmark refresh, quote assessment) driven off the
// durable queue (AC-2: "... and consumes the queue").
using Contigo.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Same configuration key and fail-fast shape as Contigo.Api/Program.cs: both hosts read the
// Documents/Contracts connection string from ConnectionStrings:DocumentsContracts so `dev`/`demo`
// Container Apps share one config-naming convention across the API and worker Container Apps
// (ADR-002, ADR-005).
var documentsContractsConnectionString = builder.Configuration.GetConnectionString("DocumentsContracts")
    ?? throw new InvalidOperationException(
        "Missing required configuration 'ConnectionStrings:DocumentsContracts' " +
        "(set env var ConnectionStrings__DocumentsContracts in deployed environments).");

// WorkerServiceCollectionExtensions.AddWorkerHost is the single source of truth for this host's
// composition (module registration + queue consumer + hosted service) -- Contigo.Worker.Tests
// calls the same method to prove the wiring, not a hand-rolled copy of it.
builder.Services.AddWorkerHost(documentsContractsConnectionString);

var host = builder.Build();
host.Run();
