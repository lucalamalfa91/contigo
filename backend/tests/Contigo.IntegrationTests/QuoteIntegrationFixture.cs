using Contigo.AiGateway;
using Contigo.AiGateway.Configuration;
using Contigo.AiGateway.Fixtures;
using Contigo.Audit.Infrastructure;
using Contigo.Documents.Contracts.Infrastructure;
using Contigo.Identity.Workspace.Infrastructure;
using Contigo.Quotes.Infrastructure;
using Contigo.SharedKernel;
using Contigo.SharedKernel.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Contigo.IntegrationTests;

/// <summary>
/// Shared fixture for task E05/F01/US01/T01 (quote-extraction) — proves parent story
/// us-01-quote-line-extraction's AC-1/AC-2/AC-4 against a real, migrated Postgres database and the
/// real <c>Contigo.Api</c> composition root, the same "one real host, no hand-rolled container"
/// shape <see cref="R1IntegrationFixture"/> already established for R1. Reuses
/// <see cref="ScriptedR1AiGateway"/> as-is (its own shape is already generic over
/// <c>AiExtractionRequest.StageName</c>, not contract-specific) rather than duplicating a
/// quote-specific scripted gateway, and <see cref="RecordingDocumentStorage"/> as-is (ADR-005/
/// ADR-011: domain code only ever sees <see cref="IDocumentStorage"/>, so one fake serves every
/// module's own upload path).
///
/// Runs every test request through a dedicated, deliberately unprivileged Postgres role (mirrors
/// <see cref="R1IntegrationFixture"/> and every per-module RLS proof in this solution): the
/// Testcontainers bootstrap role is always a superuser, and superusers unconditionally bypass row
/// security, so AC-4's cross-tenant-adjacent claims would otherwise be unverifiable as real
/// isolation (cross-tenant isolation itself is proved separately, at the module level, by
/// <c>Contigo.Quotes.Tests.QuoteRlsCrossTenantIsolationTests</c> — this fixture's own job is
/// proving the wired-up HTTP path, not re-proving RLS itself).
/// </summary>
public sealed class QuoteIntegrationFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string AppRoleName = "contigo_quotes_e2e_app";
    private const string AppRolePassword = "contigo_quotes_e2e_app_test_password";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16").Build();

    private string _appConnectionString = string.Empty;

    /// <summary>Fake <see cref="IDocumentStorage"/> the test host is wired with — reused as-is from
    /// <see cref="R1IntegrationFixture"/>'s own sibling type (ADR-005/ADR-011: domain code only
    /// ever sees the interface).</summary>
    public RecordingDocumentStorage DocumentStorage { get; } = new();

    /// <summary>The scripted gateway this fixture's host resolves <see cref="IAiGateway"/> as —
    /// exposed so a test can assert on <see cref="ScriptedR1AiGateway.OcrCallCount"/> (AC-4)
    /// without re-resolving it from the host's service provider.</summary>
    public ScriptedR1AiGateway AiGateway { get; } = new(
        new FixtureAiGateway(new AiGatewayModelOptions(), SystemClock.Instance, new AiGatewayOcrOptions()),
        QuoteExtractionScriptedPayloads.PayloadsByStage);

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var superuserConnectionString = _postgres.GetConnectionString();

        var identityOptions = new DbContextOptionsBuilder<IdentityWorkspaceDbContext>();
        IdentityWorkspaceDbContextOptions.Configure(identityOptions, superuserConnectionString);
        await using (var db = new IdentityWorkspaceDbContext(identityOptions.Options))
        {
            await db.Database.MigrateAsync();
        }

        var documentsOptions = new DbContextOptionsBuilder<DocumentsContractsDbContext>();
        DocumentsContractsDbContextOptions.Configure(documentsOptions, superuserConnectionString);
        await using (var db = new DocumentsContractsDbContext(documentsOptions.Options))
        {
            await db.Database.MigrateAsync();
        }

        var quotesOptions = new DbContextOptionsBuilder<QuotesDbContext>();
        QuotesDbContextOptions.Configure(quotesOptions, superuserConnectionString);
        await using (var db = new QuotesDbContext(quotesOptions.Options))
        {
            await db.Database.MigrateAsync();
        }

        var auditOptions = new DbContextOptionsBuilder<AuditDbContext>();
        AuditDbContextOptions.Configure(auditOptions, superuserConnectionString);
        await using (var db = new AuditDbContext(auditOptions.Options))
        {
            await db.Database.MigrateAsync();

            // One unprivileged app role, granted after every module's tables exist — same shape
            // as R1IntegrationFixture.InitializeAsync (see that method's own remarks).
            await db.Database.ExecuteSqlRawAsync(
                $"""
                CREATE ROLE {AppRoleName} LOGIN PASSWORD '{AppRolePassword}' NOSUPERUSER NOBYPASSRLS;
                GRANT USAGE ON SCHEMA public TO {AppRoleName};
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {AppRoleName};
                """);
        }

        _appConnectionString = new NpgsqlConnectionStringBuilder(superuserConnectionString)
        {
            Username = AppRoleName,
            Password = AppRolePassword,
        }.ConnectionString;
    }

    // Explicit implementation — see R0IntegrationFixture's own doc comment on why (xunit's
    // IAsyncLifetime.DisposeAsync and WebApplicationFactory<T>'s own IAsyncDisposable.DisposeAsync
    // must be disambiguated).
    Task IAsyncLifetime.DisposeAsync() => _postgres.DisposeAsync().AsTask();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:IdentityWorkspace", _appConnectionString);
        builder.UseSetting("ConnectionStrings:DocumentsContracts", _appConnectionString);
        builder.UseSetting("ConnectionStrings:Audit", _appConnectionString);
        builder.UseSetting("ConnectionStrings:Quotes", _appConnectionString);
        // No scenario in this fixture exercises Renewals/Savings — same "point at this run's own
        // Testcontainers instance rather than the static appsettings.Development.json default"
        // rationale as R1IntegrationFixture's own identical lines.
        builder.UseSetting("ConnectionStrings:Renewals", _appConnectionString);
        builder.UseSetting("ConnectionStrings:Savings", _appConnectionString);
        // Never actually dialled — IDocumentStorage is replaced with an in-memory fake below.
        builder.UseSetting("ConnectionStrings:Storage", "UseDevelopmentStorage=true");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IDocumentStorage>(DocumentStorage);

            // Overrides AddAiGatewayModule's own TryAddSingleton<IAiGateway, FixtureAiGateway>()
            // — same "append an AddSingleton override after the host's own registration" shape
            // R1IntegrationFixture already uses.
            services.AddSingleton<IAiGateway>(AiGateway);
        });
    }
}
