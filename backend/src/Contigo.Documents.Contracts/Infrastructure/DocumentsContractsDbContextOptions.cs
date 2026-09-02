using Microsoft.EntityFrameworkCore;

namespace Contigo.Documents.Contracts.Infrastructure;

/// <summary>
/// Single place that configures <see cref="DocumentsContractsDbContext"/> provider options, so
/// the runtime DI path (<see cref="ServiceCollectionExtensions"/>), the design-time factory
/// (<see cref="DocumentsContractsDbContextFactory"/>), and the integration test project can
/// never drift apart on how the Npgsql provider, pgvector plugin, and naming convention are
/// wired (ADR-003). Public so the test project (a separate assembly) can point it at a
/// disposable Testcontainers connection string instead of duplicating this setup.
/// </summary>
public static class DocumentsContractsDbContextOptions
{
    public static void Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        builder
            // .UseVector() registers the pgvector plugin on the Npgsql provider so the `vector`
            // column type and the Pgvector.Vector CLR type are recognised (ADR-003).
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            // Postgres/ADR-009 convention is snake_case (`tenant_id`, `document`, ...); without
            // this, EF Core would emit quoted PascalCase identifiers instead.
            .UseSnakeCaseNamingConvention();
    }
}
