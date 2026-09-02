// Contigo Worker Host — thin composition root (ADR-002).
// References the same domain/application libraries as the API host;
// hosts background processing (extraction, renewal recomputation,
// benchmark refresh, quote assessment).
var builder = Host.CreateApplicationBuilder(args);

// Module registration will go here as features land.
// Queue message handlers belong here, not in domain projects.

var host = builder.Build();
host.Run();
