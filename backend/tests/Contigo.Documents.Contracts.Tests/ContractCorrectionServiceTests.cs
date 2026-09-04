using System.Text.Json;
using Contigo.Documents.Contracts.Application;
using Contigo.Documents.Contracts.Domain;
using Contigo.Documents.Contracts.Infrastructure;
using Contigo.SharedKernel;
using Contigo.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Contigo.Documents.Contracts.Tests;

/// <summary>
/// Proves the Definition of Done for task E02/F05/US01/T01 (us-01-correction-history, AC-1/AC-2/
/// AC-3) and the parent story's own DoD ("dotnet test proves original extraction survives a
/// correction and history is retrievable"): <see cref="ContractCorrectionService.CorrectAsync"/>
/// records a correction as a new, append-only <see cref="ContractVersion"/>, preserves the
/// original AI extraction as version 1, and writes a queryable <see cref="CorrectionHistory"/> row
/// per changed field — against a real Postgres+RLS database, mirroring
/// <see cref="DocumentUploadServiceTests"/>'s own unprivileged-role rationale so a passing
/// cross-tenant assertion is a real RLS proof, not a tautology from a superuser connection that
/// unconditionally bypasses row security.
/// </summary>
public sealed class ContractCorrectionServiceTests : IAsyncLifetime
{
    private const string AppRoleName = "contigo_correction_app";
    private const string AppRolePassword = "contigo_correction_app_test_password";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16")
        .Build();

    private string _appConnectionString = string.Empty;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var adminOptions = new DbContextOptionsBuilder<DocumentsContractsDbContext>();
        DocumentsContractsDbContextOptions.Configure(adminOptions, _postgres.GetConnectionString());

        await using (var adminDb = new DocumentsContractsDbContext(adminOptions.Options))
        {
            // Applies Initial + AddTenantRowLevelSecurity (covers contract/contract_version/
            // correction_history — see that migration's TenantScopedTables list).
            await adminDb.Database.MigrateAsync();

            await adminDb.Database.ExecuteSqlRawAsync(
                $"""
                CREATE ROLE {AppRoleName} LOGIN PASSWORD '{AppRolePassword}' NOSUPERUSER NOBYPASSRLS;
                GRANT USAGE ON SCHEMA public TO {AppRoleName};
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {AppRoleName};
                """);
        }

        _appConnectionString = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Username = AppRoleName,
            Password = AppRolePassword,
        }.ConnectionString;
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private DocumentsContractsDbContext CreateAppContext(ITenantContext tenantContext)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DocumentsContractsDbContext>();
        DocumentsContractsDbContextOptions.Configure(optionsBuilder, _appConnectionString, tenantContext);
        return new DocumentsContractsDbContext(optionsBuilder.Options);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    /// <summary>Seeds a <see cref="Contract"/> row directly — this module has no "create
    /// contract" writer yet (that is the extraction pipeline's job, out of this task's scope); a
    /// directly-seeded row stands in for "whatever the AI extraction originally wrote", which is
    /// exactly the state <see cref="ContractCorrectionService"/> must treat as the original
    /// extraction to preserve (AC-2).</summary>
    private static async Task<EntityId> SeedContractAsync(
        DocumentsContractsDbContext db, TenantId tenantId, DateTimeOffset createdAt)
    {
        var contract = new Contract
        {
            TenantId = tenantId,
            Type = ContractDocumentType.Msa,
            Status = "needs_review",
            Currency = "USD",
            AnnualSpend = 100000m,
            AutoRenewal = false,
            CreatedAt = createdAt,
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
        return contract.Id;
    }

    /// <summary>Reads a snapshot field back as text regardless of its underlying JSON token type
    /// (<see cref="ContractCorrectionService"/>'s snapshot serializes <c>decimal</c>/<c>bool</c>
    /// fields as JSON numbers/booleans, not strings — <see cref="JsonElement.ToString"/> renders
    /// any of them as plain text, unlike <see cref="JsonElement.GetString"/> which only accepts a
    /// JSON string token).</summary>
    private static string? SnapshotField(string snapshotJson, string propertyName)
    {
        using var document = JsonDocument.Parse(snapshotJson);
        return document.RootElement.TryGetProperty(propertyName, out var value)
            && value.ValueKind != JsonValueKind.Null
                ? value.ToString()
                : null;
    }

    [Fact]
    public async Task First_correction_preserves_the_original_extraction_as_version_one_and_appends_version_two()
    {
        var tenantId = TenantId.New();
        var now = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        var tenantContext = new TenantContext();

        EntityId contractId;
        using (tenantContext.BeginScope(tenantId))
        {
            await using var seedDb = CreateAppContext(tenantContext);
            contractId = await SeedContractAsync(seedDb, tenantId, now.AddDays(-1));
        }

        await using var db = CreateAppContext(tenantContext);
        var service = new ContractCorrectionService(db, tenantContext, new FixedClock(now));

        var result = await service.CorrectAsync(
            tenantId,
            contractId,
            new Dictionary<string, string?> { ["annualSpend"] = "125000.00" },
            "Corrected misread OCR amount");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.VersionNumber);
        Assert.Equal(["annualSpend"], result.Value.CorrectedFields);
        Assert.Equal(now, result.Value.CorrectedAt);

        using (tenantContext.BeginScope(tenantId))
        {
            await using var readDb = CreateAppContext(tenantContext);

            // AC-2: original extraction preserved as version 1 — untouched by the correction.
            var versions = await readDb.ContractVersions
                .Where(v => v.ContractId == contractId)
                .OrderBy(v => v.VersionNumber)
                .ToListAsync();
            Assert.Equal(2, versions.Count);
            Assert.Equal(1, versions[0].VersionNumber);
            // The seeded 100000m literal round-trips through Postgres's numeric(18,2) AnnualSpend
            // column (ContractConfiguration.HasPrecision(18, 2)) as a scale-2 decimal, so both the
            // snapshot and the history's PreviousValue below read back "100000.00", not "100000".
            Assert.Equal("100000.00", SnapshotField(versions[0].SnapshotJson, "AnnualSpend"));
            Assert.Equal(2, versions[1].VersionNumber);
            Assert.Equal("125000.00", SnapshotField(versions[1].SnapshotJson, "AnnualSpend"));

            // AC-1/AC-3: the correction itself is queryable, versioned history — not a silent overwrite.
            var history = await readDb.CorrectionHistories.SingleAsync(h => h.TargetEntityId == contractId);
            Assert.Equal(nameof(Contract), history.TargetEntityType);
            Assert.Equal("annualSpend", history.FieldName);
            Assert.Equal("100000.00", history.PreviousValue);
            Assert.Equal("125000.00", history.NewValue);
            Assert.Equal("Corrected misread OCR amount", history.Reason);

            // Live row reflects the corrected value.
            var contract = await readDb.Contracts.SingleAsync(c => c.Id == contractId);
            Assert.Equal(125000.00m, contract.AnnualSpend);
        }
    }

    [Fact]
    public async Task A_second_correction_appends_version_three_and_leaves_versions_one_and_two_untouched()
    {
        var tenantId = TenantId.New();
        var now = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        var tenantContext = new TenantContext();

        EntityId contractId;
        using (tenantContext.BeginScope(tenantId))
        {
            await using var seedDb = CreateAppContext(tenantContext);
            contractId = await SeedContractAsync(seedDb, tenantId, now.AddDays(-1));
        }

        await using (var db = CreateAppContext(tenantContext))
        {
            var service = new ContractCorrectionService(db, tenantContext, new FixedClock(now));
            var first = await service.CorrectAsync(
                tenantId, contractId, new Dictionary<string, string?> { ["status"] = "active" }, "First fix");
            Assert.True(first.IsSuccess);
            Assert.Equal(2, first.Value.VersionNumber);
        }

        await using (var db = CreateAppContext(tenantContext))
        {
            var service = new ContractCorrectionService(db, tenantContext, new FixedClock(now.AddHours(1)));
            var second = await service.CorrectAsync(
                tenantId, contractId, new Dictionary<string, string?> { ["status"] = "expired" }, "Second fix");
            Assert.True(second.IsSuccess);
            Assert.Equal(3, second.Value.VersionNumber);
        }

        using (tenantContext.BeginScope(tenantId))
        {
            await using var readDb = CreateAppContext(tenantContext);

            var versions = await readDb.ContractVersions
                .Where(v => v.ContractId == contractId)
                .OrderBy(v => v.VersionNumber)
                .ToListAsync();
            Assert.Equal(3, versions.Count);
            Assert.Equal("needs_review", SnapshotField(versions[0].SnapshotJson, "Status"));
            Assert.Equal("active", SnapshotField(versions[1].SnapshotJson, "Status"));
            Assert.Equal("expired", SnapshotField(versions[2].SnapshotJson, "Status"));

            var histories = await readDb.CorrectionHistories
                .Where(h => h.TargetEntityId == contractId)
                .OrderBy(h => h.CorrectedAt)
                .ToListAsync();
            Assert.Equal(2, histories.Count);
            Assert.Equal("needs_review", histories[0].PreviousValue);
            Assert.Equal("active", histories[0].NewValue);
            Assert.Equal("active", histories[1].PreviousValue);
            Assert.Equal("expired", histories[1].NewValue);
        }
    }

    [Fact]
    public async Task An_invalid_field_value_fails_the_whole_request_and_writes_nothing()
    {
        var tenantId = TenantId.New();
        var now = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        var tenantContext = new TenantContext();

        EntityId contractId;
        using (tenantContext.BeginScope(tenantId))
        {
            await using var seedDb = CreateAppContext(tenantContext);
            contractId = await SeedContractAsync(seedDb, tenantId, now);
        }

        await using var db = CreateAppContext(tenantContext);
        var service = new ContractCorrectionService(db, tenantContext, new FixedClock(now));

        // 'status' alone would be a valid, genuine change; 'startDate' is garbage. The whole
        // request must fail — including the field that would otherwise have been valid alone.
        var result = await service.CorrectAsync(
            tenantId,
            contractId,
            new Dictionary<string, string?> { ["status"] = "active", ["startDate"] = "not-a-date" },
            reason: null);

        Assert.True(result.IsFailure);

        using (tenantContext.BeginScope(tenantId))
        {
            await using var readDb = CreateAppContext(tenantContext);

            // Proves the validate-before-mutate split actually works: no baseline version, no
            // history, and the live row's Status is still the original seed value — not half-applied.
            Assert.Empty(await readDb.ContractVersions.Where(v => v.ContractId == contractId).ToListAsync());
            Assert.Empty(await readDb.CorrectionHistories.Where(h => h.TargetEntityId == contractId).ToListAsync());
            var contract = await readDb.Contracts.SingleAsync(c => c.Id == contractId);
            Assert.Equal("needs_review", contract.Status);
        }
    }

    [Fact]
    public async Task An_unknown_field_name_fails_and_writes_nothing()
    {
        var tenantId = TenantId.New();
        var now = DateTimeOffset.UtcNow;
        var tenantContext = new TenantContext();

        EntityId contractId;
        using (tenantContext.BeginScope(tenantId))
        {
            await using var seedDb = CreateAppContext(tenantContext);
            contractId = await SeedContractAsync(seedDb, tenantId, now);
        }

        await using var db = CreateAppContext(tenantContext);
        var service = new ContractCorrectionService(db, tenantContext, new FixedClock(now));

        var result = await service.CorrectAsync(
            tenantId, contractId, new Dictionary<string, string?> { ["notAField"] = "x" }, reason: null);

        Assert.True(result.IsFailure);
        Assert.Contains("notAField", result.Error);

        using (tenantContext.BeginScope(tenantId))
        {
            await using var readDb = CreateAppContext(tenantContext);
            Assert.Empty(await readDb.ContractVersions.Where(v => v.ContractId == contractId).ToListAsync());
        }
    }

    [Fact]
    public async Task Correcting_to_the_current_value_is_rejected_as_a_no_op_and_writes_nothing()
    {
        var tenantId = TenantId.New();
        var now = DateTimeOffset.UtcNow;
        var tenantContext = new TenantContext();

        EntityId contractId;
        using (tenantContext.BeginScope(tenantId))
        {
            await using var seedDb = CreateAppContext(tenantContext);
            contractId = await SeedContractAsync(seedDb, tenantId, now);
        }

        await using var db = CreateAppContext(tenantContext);
        var service = new ContractCorrectionService(db, tenantContext, new FixedClock(now));

        var result = await service.CorrectAsync(
            tenantId,
            contractId,
            new Dictionary<string, string?> { ["status"] = "needs_review" }, // same as seeded value
            reason: null);

        Assert.True(result.IsFailure);

        using (tenantContext.BeginScope(tenantId))
        {
            await using var readDb = CreateAppContext(tenantContext);

            // Not even the version-1 baseline should be written — a no-op request has zero side
            // effects, regardless of whether the contract had prior history.
            Assert.Empty(await readDb.ContractVersions.Where(v => v.ContractId == contractId).ToListAsync());
        }
    }

    [Fact]
    public async Task Correcting_a_contract_that_does_not_exist_fails_with_the_not_found_error()
    {
        var tenantId = TenantId.New();
        var tenantContext = new TenantContext();
        await using var db = CreateAppContext(tenantContext);
        var service = new ContractCorrectionService(db, tenantContext, new FixedClock(DateTimeOffset.UtcNow));

        var result = await service.CorrectAsync(
            tenantId, EntityId.New(), new Dictionary<string, string?> { ["status"] = "active" }, reason: null);

        Assert.True(result.IsFailure);
        Assert.Equal(ContractCorrectionService.ContractNotFoundError, result.Error);
    }

    [Fact]
    public async Task A_different_tenant_cannot_correct_another_tenants_contract()
    {
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();
        var tenantContext = new TenantContext();
        var now = DateTimeOffset.UtcNow;

        EntityId contractId;
        using (tenantContext.BeginScope(tenantA))
        {
            await using var seedDb = CreateAppContext(tenantContext);
            contractId = await SeedContractAsync(seedDb, tenantA, now);
        }

        await using var db = CreateAppContext(tenantContext);
        var service = new ContractCorrectionService(db, tenantContext, new FixedClock(now));

        // ADR-009: tenant B's request scopes the connection to tenant B; tenant A's row genuinely
        // exists (seeded above) but must be invisible — both the app-level tenant predicate and
        // Postgres RLS independently deny it, so this proves a real cross-tenant guarantee, not a
        // vacuous pass from a superuser connection that unconditionally bypasses row security.
        var result = await service.CorrectAsync(
            tenantB, contractId, new Dictionary<string, string?> { ["status"] = "active" }, reason: null);

        Assert.True(result.IsFailure);
        Assert.Equal(ContractCorrectionService.ContractNotFoundError, result.Error);
    }
}
