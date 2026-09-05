using Contigo.Savings.Domain;
using Contigo.Savings.Infrastructure.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Contigo.Savings.Infrastructure;

/// <summary>
/// EF Core DbContext for the Savings bounded context (ADR-003; task E04/F02/US02/T01,
/// savings-opportunity — this module's first <c>DbContext</c>). Postgres via npgsql is the only
/// access path; schema changes flow through code-first migrations only (no hand-edited DDL). RLS
/// policies and the ambient per-request tenant claim are wired the same way
/// <c>Contigo.Renewals.Infrastructure.RenewalsDbContext</c> /
/// <c>Contigo.Audit.Infrastructure.AuditDbContext</c> already wire them — this context only shapes
/// the model and exposes the DbSet.
/// </summary>
public sealed class SavingsDbContext(DbContextOptions<SavingsDbContext> options) : DbContext(options)
{
    public DbSet<SavingsOpportunity> SavingsOpportunities => Set<SavingsOpportunity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new SavingsOpportunityConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
