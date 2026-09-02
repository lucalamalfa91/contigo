// Contigo API Host — thin composition root (ADR-002).
// Wires all modules via DI; contains no business logic.
var builder = WebApplication.CreateBuilder(args);

// Module registration will go here as features land.
// Each module exposes an AddXxx(IServiceCollection) extension method.

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
