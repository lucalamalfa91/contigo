using Contigo.Savings.Application;
using Contigo.Savings.Domain;
using Contigo.Savings.Infrastructure;
using Contigo.Savings.Tests.TestSupport;
using Contigo.SharedKernel;
using Contigo.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Contigo.Savings.Tests;

/// <summary>
/// Proves the Definition of Done for task E04/F02/US02/T01 (savings-opportunity; parent story
/// us-02-savings-opportunity AC-1/AC-2, and the story's own DoD "opportunity lifecycle (identify
/// -&gt; approve -&gt; realized)" up to the point this task owns — see
/// <see cref="SavingsOpportunityStatus.Realized"/>'s own doc comment for the audit-tracked
/// realized-value gap task E04/F02/US02/T02 closes): <see cref="SavingsOpportunityService"/>
/// genuinely persists an identified opportunity as a readable row through the exact same
/// Npgsql/EF Core/RLS-interceptor pipeline every other module's write path uses (ADR-003/ADR-009),
/// validates its fields before writing anything, supports a partial owner/status update by id, and
/// writes one <see cref="IAuditWriter"/> entry per successful mutation (spec §14.1). Mirrors
/// <c>Contigo.Renewals.Tests.RenewalActionServiceTests</c>'s own Testcontainers shape;
/// tenant-isolation-under-RLS is proved separately by
/// <c>SavingsOpportunityRlsCrossTenantIsolationTests</c>.
///
/// Spins up its own disposable Postgres container per test run (Testcontainers), so this test
/// needs nothing but a running Docker daemon; no shared/external database to stand up by hand.
/// </summary>
public sealed class SavingsOpportunityServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private SavingsDbContext CreateContext(ITenantContext? tenantContext = null)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SavingsDbContext>();
        SavingsDbContextOptions.Configure(optionsBuilder, _postgres.GetConnectionString(), tenantContext);
        return new SavingsDbContext(optionsBuilder.Options);
    }

    private async Task MigrateAsync()
    {
        await using var migrateDb = CreateContext();
        await migrateDb.Database.MigrateAsync();
    }

    private static CreateSavingsOpportunityRequest ValidRequest(
        EntityId? supplierId = null, EntityId? contractId = null) =>
        new(
            SupplierId: supplierId,
            ContractId: contractId,
            Type: "price-renegotiation",
            CurrentSpend: 120_000m,
            Currency: "USD",
            EstimatedSavingsLow: 8_000m,
            EstimatedSavingsHigh: 15_000m,
            Confidence: 0.72);

    [Fact]
    public async Task CreateAsync_persists_an_identified_opportunity_readable_through_ListAsync()
    {
        await MigrateAsync();

        var tenantId = TenantId.New();
        var supplierId = EntityId.New();
        var contractId = EntityId.New();
        var tenantContext = new TenantContext();
        var clock = new FixedClock(new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero));
        var auditWriter = new RecordingAuditWriter();

        EntityId opportunityId;
        await using (var db = CreateContext(tenantContext))
        {
            var service = new SavingsOpportunityService(db, tenantContext, clock, auditWriter);

            var result = await service.CreateAsync(tenantId, ValidRequest(supplierId, contractId));

            Assert.True(result.IsSuccess);
            Assert.Equal(supplierId, result.Value.SupplierId);
            Assert.Equal(contractId, result.Value.ContractId);
            Assert.Equal("price-renegotiation", result.Value.Type);
            Assert.Equal(120_000m, result.Value.CurrentSpend);
            Assert.Equal("USD", result.Value.Currency);
            Assert.Equal(8_000m, result.Value.EstimatedSavingsLow);
            Assert.Equal(15_000m, result.Value.EstimatedSavingsHigh);
            Assert.Equal(0.72, result.Value.Confidence);
            Assert.Equal(SavingsOpportunityStatus.Identified, result.Value.Status);
            Assert.Null(result.Value.Owner);
            Assert.Equal(clock.UtcNow, result.Value.CreatedAt);
            Assert.Equal(clock.UtcNow, result.Value.UpdatedAt);
            opportunityId = result.Value.Id;
        }

        // Fresh context/connection, same tenant scope: this reads back from Postgres, not the
        // change tracker, and only succeeds if RLS actually lets this tenant see its own row.
        await using var readDb = CreateContext(tenantContext);
        var readService = new SavingsOpportunityService(readDb, tenantContext, clock, auditWriter);
        var listed = await readService.ListAsync(tenantId);

        var row = Assert.Single(listed);
        Assert.Equal(opportunityId, row.Id);

        // Spec §14.1 / Appendix C rule 9: every successful write is audited.
        var entry = Assert.Single(auditWriter.Written);
        Assert.Equal(tenantId, entry.TenantId);
        Assert.Equal("savings_opportunity.identified", entry.Action);
        Assert.Equal("savings_opportunity", entry.ResourceType);
        Assert.Equal(opportunityId.Value.ToString(), entry.ResourceId);
    }

    [Fact]
    public async Task ListAsync_returns_only_this_tenants_opportunities_newest_first()
    {
        await MigrateAsync();

        var tenantId = TenantId.New();
        var otherTenantId = TenantId.New();
        var tenantContext = new TenantContext();
        var auditWriter = new RecordingAuditWriter();

        await using (var db = CreateContext(tenantContext))
        {
            var earlyClock = new FixedClock(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
            var lateClock = new FixedClock(new DateTimeOffset(2026, 9, 4, 0, 0, 0, TimeSpan.Zero));

            var earlyService = new SavingsOpportunityService(db, tenantContext, earlyClock, auditWriter);
            var lateService = new SavingsOpportunityService(db, tenantContext, lateClock, auditWriter);

            var first = await earlyService.CreateAsync(tenantId, ValidRequest());
            var second = await lateService.CreateAsync(tenantId, ValidRequest());
            await lateService.CreateAsync(otherTenantId, ValidRequest());

            await using var readDb = CreateContext(tenantContext);
            var readService = new SavingsOpportunityService(readDb, tenantContext, lateClock, auditWriter);
            var listed = await readService.ListAsync(tenantId);

            Assert.Equal(2, listed.Count);
            // Newest identified first.
            Assert.Equal(second.Value.Id, listed[0].Id);
            Assert.Equal(first.Value.Id, listed[1].Id);
        }
    }

    [Fact]
    public async Task UpdateAsync_changes_only_the_supplied_fields()
    {
        await MigrateAsync();

        var tenantId = TenantId.New();
        var tenantContext = new TenantContext();
        var auditWriter = new RecordingAuditWriter();
        var createClock = new FixedClock(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        var updateClock = new FixedClock(new DateTimeOffset(2026, 9, 4, 0, 0, 0, TimeSpan.Zero));

        EntityId opportunityId;
        await using (var db = CreateContext(tenantContext))
        {
            var createService = new SavingsOpportunityService(db, tenantContext, createClock, auditWriter);
            var created = await createService.CreateAsync(tenantId, ValidRequest());
            opportunityId = created.Value.Id;
        }

        await using (var db = CreateContext(tenantContext))
        {
            var updateService = new SavingsOpportunityService(db, tenantContext, updateClock, auditWriter);

            // Owner only — status must remain Identified.
            var ownerUpdate = await updateService.UpdateAsync(
                tenantId, opportunityId, owner: "alice@acme.example", status: null);

            Assert.True(ownerUpdate.IsSuccess);
            Assert.Equal("alice@acme.example", ownerUpdate.Value.Owner);
            Assert.Equal(SavingsOpportunityStatus.Identified, ownerUpdate.Value.Status);
            Assert.Equal(updateClock.UtcNow, ownerUpdate.Value.UpdatedAt);

            // Status only — owner set above must survive untouched.
            var statusUpdate = await updateService.UpdateAsync(
                tenantId, opportunityId, owner: null, status: "InProgress");

            Assert.True(statusUpdate.IsSuccess);
            Assert.Equal("alice@acme.example", statusUpdate.Value.Owner);
            Assert.Equal(SavingsOpportunityStatus.InProgress, statusUpdate.Value.Status);
        }

        Assert.Equal(3, auditWriter.Written.Count); // 1 identify + 2 updates.
        Assert.All(
            auditWriter.Written.Skip(1),
            entry => Assert.Equal("savings_opportunity.updated", entry.Action));
    }

    [Fact]
    public async Task UpdateAsync_can_move_a_status_all_the_way_to_Realized()
    {
        await MigrateAsync();

        var tenantId = TenantId.New();
        var tenantContext = new TenantContext();
        var auditWriter = new RecordingAuditWriter();
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        await using var db = CreateContext(tenantContext);
        var service = new SavingsOpportunityService(db, tenantContext, clock, auditWriter);
        var created = await service.CreateAsync(tenantId, ValidRequest());

        await service.UpdateAsync(tenantId, created.Value.Id, owner: "bob@acme.example", status: "InProgress");
        var realized = await service.UpdateAsync(tenantId, created.Value.Id, owner: null, status: "Realized");

        Assert.True(realized.IsSuccess);
        Assert.Equal(SavingsOpportunityStatus.Realized, realized.Value.Status);
    }

    [Fact]
    public async Task UpdateAsync_returns_NotFoundError_for_an_unknown_id()
    {
        await MigrateAsync();

        var tenantContext = new TenantContext();
        await using var db = CreateContext(tenantContext);
        var service = new SavingsOpportunityService(
            db, tenantContext, new FixedClock(DateTimeOffset.UtcNow), new RecordingAuditWriter());

        var result = await service.UpdateAsync(TenantId.New(), EntityId.New(), owner: "alice", status: null);

        Assert.True(result.IsFailure);
        Assert.Equal(SavingsOpportunityService.NotFoundError, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_rejects_a_request_with_neither_field_without_writing_anything()
    {
        await MigrateAsync();

        var tenantId = TenantId.New();
        var tenantContext = new TenantContext();
        var auditWriter = new RecordingAuditWriter();
        await using var db = CreateContext(tenantContext);
        var service = new SavingsOpportunityService(db, tenantContext, new FixedClock(DateTimeOffset.UtcNow), auditWriter);

        var created = await service.CreateAsync(tenantId, ValidRequest());
        var writesBeforeUpdate = auditWriter.Written.Count;

        var result = await service.UpdateAsync(tenantId, created.Value.Id, owner: null, status: null);

        Assert.True(result.IsFailure);
        Assert.Equal(SavingsOpportunityService.NoFieldsToUpdateError, result.Error);
        Assert.Equal(writesBeforeUpdate, auditWriter.Written.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_rejects_a_blank_owner(string blankOwner)
    {
        await MigrateAsync();

        var tenantId = TenantId.New();
        var tenantContext = new TenantContext();
        await using var db = CreateContext(tenantContext);
        var service = new SavingsOpportunityService(
            db, tenantContext, new FixedClock(DateTimeOffset.UtcNow), new RecordingAuditWriter());

        var created = await service.CreateAsync(tenantId, ValidRequest());

        var result = await service.UpdateAsync(tenantId, created.Value.Id, owner: blankOwner, status: null);

        Assert.True(result.IsFailure);
        Assert.Equal(SavingsOpportunityService.OwnerCannotBeBlankError, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_rejects_an_unrecognized_status()
    {
        await MigrateAsync();

        var tenantId = TenantId.New();
        var tenantContext = new TenantContext();
        await using var db = CreateContext(tenantContext);
        var service = new SavingsOpportunityService(
            db, tenantContext, new FixedClock(DateTimeOffset.UtcNow), new RecordingAuditWriter());

        var created = await service.CreateAsync(tenantId, ValidRequest());

        var result = await service.UpdateAsync(
            tenantId, created.Value.Id, owner: null, status: "Cancelled");

        Assert.True(result.IsFailure);
        Assert.Equal(SavingsOpportunityService.StatusInvalidError, result.Error);
    }

    // ----- CreateAsync validation -----

    [Fact]
    public async Task CreateAsync_rejects_a_blank_type_without_writing_anything()
    {
        await MigrateAsync();

        var tenantContext = new TenantContext();
        var auditWriter = new RecordingAuditWriter();
        await using var db = CreateContext(tenantContext);
        var service = new SavingsOpportunityService(db, tenantContext, new FixedClock(DateTimeOffset.UtcNow), auditWriter);

        var request = ValidRequest() with { Type = "  " };
        var result = await service.CreateAsync(TenantId.New(), request);

        Assert.True(result.IsFailure);
        Assert.Equal(SavingsOpportunityService.TypeRequiredError, result.Error);
        Assert.Empty(auditWriter.Written);
        Assert.Equal(0, await db.SavingsOpportunities.CountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task CreateAsync_rejects_a_non_positive_current_spend(decimal currentSpend)
    {
        await MigrateAsync();

        var tenantContext = new TenantContext();
        await using var db = CreateContext(tenantContext);
        var service = new SavingsOpportunityService(
            db, tenantContext, new FixedClock(DateTimeOffset.UtcNow), new RecordingAuditWriter());

        var request = ValidRequest() with { CurrentSpend = currentSpend };
        var result = await service.CreateAsync(TenantId.New(), request);

        Assert.True(result.IsFailure);
        Assert.Equal(SavingsOpportunityService.CurrentSpendMustBePositiveError, result.Error);
    }

    [Fact]
    public async Task CreateAsync_rejects_an_estimated_savings_range_where_high_is_below_low()
    {
        await MigrateAsync();

        var tenantContext = new TenantContext();
        await using var db = CreateContext(tenantContext);
        var service = new SavingsOpportunityService(
            db, tenantContext, new FixedClock(DateTimeOffset.UtcNow), new RecordingAuditWriter());

        var request = ValidRequest() with { EstimatedSavingsLow = 10_000m, EstimatedSavingsHigh = 5_000m };
        var result = await service.CreateAsync(TenantId.New(), request);

        Assert.True(result.IsFailure);
        Assert.Equal(SavingsOpportunityService.EstimatedSavingsRangeInvalidError, result.Error);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public async Task CreateAsync_rejects_a_confidence_outside_zero_to_one(double confidence)
    {
        await MigrateAsync();

        var tenantContext = new TenantContext();
        await using var db = CreateContext(tenantContext);
        var service = new SavingsOpportunityService(
            db, tenantContext, new FixedClock(DateTimeOffset.UtcNow), new RecordingAuditWriter());

        var request = ValidRequest() with { Confidence = confidence };
        var result = await service.CreateAsync(TenantId.New(), request);

        Assert.True(result.IsFailure);
        Assert.Equal(SavingsOpportunityService.ConfidenceOutOfRangeError, result.Error);
    }

    [Fact]
    public async Task CreateAsync_rejects_a_null_request_argument()
    {
        await MigrateAsync();

        var tenantContext = new TenantContext();
        await using var db = CreateContext(tenantContext);
        var service = new SavingsOpportunityService(
            db, tenantContext, new FixedClock(DateTimeOffset.UtcNow), new RecordingAuditWriter());

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.CreateAsync(TenantId.New(), null!));
    }
}
