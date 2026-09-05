using System.Net;
using System.Text.Json;
using Contigo.Audit.Infrastructure;
using Contigo.Documents.Contracts.Domain;
using Contigo.Renewals.Application;
using Contigo.Renewals.Domain;
using Contigo.SharedKernel;
using Contigo.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Contigo.IntegrationTests;

/// <summary>
/// Proves the Definition of Done for task E03/F04/US01/T01 (r2-integration) and its parent story
/// us-01-final-integration: AC-1 ("Deterministic renewal/cancellation for every active contract
/// (where data exists)"), AC-2 ("Threshold events fire and recommendations do not invent dates")
/// and the pipeline/insight-card half of AC-3 ("Pipeline + insight card work on `demo` with tenant
/// isolation" — see <see cref="R2CrossTenantIsolationTests"/> for the isolation half, mirroring how
/// R1 split <see cref="R1EndToEndTests"/>/<see cref="R1CrossTenantIsolationTests"/>) — driven over
/// real HTTP through the real <c>Contigo.Api</c> composition root, against a real, migrated
/// Postgres+pgvector+RLS database (see <see cref="R2IntegrationFixture"/>).
///
/// R2's own leaf artifacts (this task's own <c>depends_on</c>: <c>renewal-opportunity</c>,
/// <c>renewal-priority-explain</c>, <c>renewal-alerts</c>, <c>renewal-action</c>) all take
/// already-validated contract data as an input; none of them produces it. So unlike
/// <see cref="R1EndToEndTests"/> (which proves the upload -&gt; extraction path that *creates* a
/// contract), this test seeds contracts directly against the real, RLS-enforced
/// <c>DocumentsContractsDbContext</c> (see <see cref="R2IntegrationFixture.SeedContractAsync"/>),
/// then proves the Renewals layer end to end on top of it. Reads response bodies as raw
/// <see cref="JsonElement"/>s rather than typed DTOs — same reason as
/// <see cref="R0EndToEndTests"/>/<see cref="R1EndToEndTests"/>'s own doc comments. Reuses
/// <see cref="R1EndToEndTests"/>'s <c>GetAsync</c>/<c>PostAsync</c>/<c>ParseAsync</c> helpers rather
/// than duplicating them — the same cross-class reuse <see cref="R1CrossTenantIsolationTests"/>
/// already established for this test assembly (they are generic HTTP plumbing, not R1-specific).
///
/// <para>
/// <b>Honest scope note (renewal-alerts):</b> this task's own wave-spec <c>depends_on</c> names
/// <c>renewal-alerts</c> (task E03/F02/US01/T02, "Alert creation + re-compute on correction"), but
/// that task's own branch carries zero commits beyond `integration` as of this task's run (checked
/// via this task's own git history read, not assumed) — it has not landed any code. Per this
/// repo's own <c>backend/README.md</c> ("Renewal threshold scheduler" section) and
/// <see cref="RenewalThresholdScheduler"/>'s own doc comment, the only "alert" artifact that
/// actually exists today is the <c>renewal.approaching</c> <em>threshold event</em> (task
/// E03/F02/US01/T01, <c>threshold-scheduler</c>) — a durable, queryable
/// <see cref="Contigo.SharedKernel.IAuditWriter"/> entry, not a persisted, de-duplicated
/// <c>RenewalAlert</c> row with recompute-on-correction. This test proves exactly that — parent
/// story AC-2's own literal wording is "Threshold events fire", and this is the artifact that
/// fires them — and no more: a persisted alert entity with recompute-on-correction remains task
/// E03/F02/US01/T02's own, still-open file scope, not silently absorbed here (this task's own "do
/// not touch unrelated wave artifacts" instruction).
/// </para>
///
/// <para>
/// While proving AC-2 for real against this fixture's deliberately unprivileged, <c>NOBYPASSRLS</c>
/// Postgres role (see <see cref="R2IntegrationFixture"/>'s own doc comment), this task found and
/// fixed a real gap in the already-merged <c>threshold-scheduler</c> artifact:
/// <see cref="RenewalThresholdScheduler.EvaluateThresholdsAsync"/> wrote its
/// <c>renewal.approaching</c> audit entry without ever opening an <see cref="ITenantContext"/>
/// scope, so <c>TenantRlsConnectionInterceptor</c> left `app.tenant_id` unset and the Audit
/// module's own `AddTenantRowLevelSecurity` `WITH CHECK` policy rejected the insert outright — a
/// scheduler run that actually raised an event would throw, not "fire silently wrong". Neither
/// <c>Contigo.Renewals.Tests.RenewalThresholdSchedulerTests</c> (a <c>RecordingAuditWriter</c>, no
/// database) nor <c>Contigo.Worker.Tests.RenewalThresholdSchedulerHostedServiceTests</c> (a
/// syntactically-valid-but-never-dialled connection string, by its own design) ever exercised a
/// real RLS-enforced connection on this path, so this was previously undetected.
/// <see cref="RenewalThresholdScheduler"/> now opens its own scope, the same convention
/// <c>Contigo.Renewals.Application.RenewalActionService.SetActionAsync</c> already follows — the
/// threshold-scheduler assertions below are the proof the fix actually works end to end.
/// </para>
/// </summary>
public sealed class R2EndToEndTests : IClassFixture<R2IntegrationFixture>
{
    private readonly R2IntegrationFixture _fixture;

    public R2EndToEndTests(R2IntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Dates_priority_alerts_and_action_compose_into_one_prioritized_pipeline()
    {
        var client = _fixture.CreateClient();
        var tenantId = TenantId.New();
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        // Contract A: near-term, high spend, a known raw cancellation-deadline fact, High risk —
        // expected to lead the pipeline and to cross a configured threshold window (AC-1, AC-2).
        var contractA = await _fixture.SeedContractAsync(
            tenantId, supplierId: EntityId.New(), annualSpend: 300_000m,
            endDate: today.AddDays(90), cancellationDeadline: today.AddDays(60),
            autoRenewal: true, risk: RiskSeverity.High);

        // Contract B: far out, low spend, no independently-known cancellation deadline — expected
        // to sort after A and to raise no threshold event (400 is not one of the default
        // 365/270/180/120/90/60/30-day windows).
        var contractB = await _fixture.SeedContractAsync(
            tenantId, annualSpend: 10_000m, endDate: today.AddDays(400), autoRenewal: true);

        // Contract C: AutoRenewal is true but EndDate is unknown — AC-1's own "(where data exists)"
        // boundary and AC-2's "recommendations do not invent dates": every layer below must abstain
        // honestly instead of guessing (Appendix C rule 10).
        var contractC = await _fixture.SeedContractAsync(tenantId, autoRenewal: true, endDate: null);

        // ----- GET /api/renewals: prioritized pipeline + insight card (AC-1, AC-3) -----

        var pipelineResponse = await R1EndToEndTests.GetAsync(client, "/api/renewals", tenantId.Value);
        Assert.Equal(HttpStatusCode.OK, pipelineResponse.StatusCode);
        var pipelineBody = await R1EndToEndTests.ParseAsync(pipelineResponse);
        var items = pipelineBody.GetProperty("items").EnumerateArray().ToList();

        // Most-urgent-first: A (60 days to its known cancellation deadline) before B (400 days to
        // its renewal date, no cancellation deadline known) before C (nothing determinable at all).
        Assert.Equal(
            new[] { contractA.Id.Value, contractB.Id.Value, contractC.Id.Value },
            items.Select(i => i.GetProperty("contractId").GetGuid()).ToArray());

        var itemA = items[0];
        Assert.Equal("Determined", itemA.GetProperty("status").GetString());
        Assert.Equal(today.AddDays(90), DateOnly.Parse(itemA.GetProperty("renewalDate").GetString()!));
        Assert.Equal(90, itemA.GetProperty("daysUntilRenewal").GetInt32());
        Assert.Equal(60, itemA.GetProperty("daysUntilCancellationDeadline").GetInt32());
        Assert.Equal("Start negotiation now", itemA.GetProperty("action").GetString());
        Assert.Equal(300_000m, itemA.GetProperty("annualSpend").GetDecimal());

        var itemB = items[1];
        Assert.Equal("Determined", itemB.GetProperty("status").GetString());
        Assert.Equal(400, itemB.GetProperty("daysUntilRenewal").GetInt32());
        Assert.Equal(JsonValueKind.Null, itemB.GetProperty("daysUntilCancellationDeadline").ValueKind);
        Assert.Equal("Monitor — no action needed yet", itemB.GetProperty("action").GetString());

        // AC-2 "do not invent dates": C never gets a fabricated renewal date just because the
        // pipeline needs to show *something*.
        var itemC = items[2];
        Assert.Equal("CannotDetermine", itemC.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, itemC.GetProperty("renewalDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, itemC.GetProperty("daysUntilRenewal").ValueKind);
        Assert.Equal("Review contract — missing end date", itemC.GetProperty("action").GetString());

        // Insight card separates facts from recommendations (AC-3, spec §9.3) for the lead row.
        var insightCard = itemA.GetProperty("insightCard");
        Assert.Equal(300_000m, insightCard.GetProperty("facts").GetProperty("annualSpend").GetDecimal());
        Assert.Equal(
            "Start negotiation now",
            insightCard.GetProperty("recommendations").GetProperty("recommendedAction").GetString());

        // ----- GET /api/renewals/{id}/priority: explainable, component-scored priority
        //       (renewal-priority-explain) -----

        var priorityAResponse = await R1EndToEndTests.GetAsync(
            client, $"/api/renewals/{contractA.Id.Value}/priority", tenantId.Value);
        Assert.Equal(HttpStatusCode.OK, priorityAResponse.StatusCode);
        var priorityABody = await R1EndToEndTests.ParseAsync(priorityAResponse);
        var componentsA = priorityABody.GetProperty("components");

        // Exact numbers under the spec-default weights (PriorityScoreWeightsOptions, max 20 each):
        // spend >=250k -> 0.8*20=16; 90 days until renewal -> 0.75*20=15; no benchmark data ->
        // neutral 10; no uplift data -> 0; High contract risk -> 0.75*20=15. Total 16+15+10+0+15=56.
        Assert.Equal(16m, componentsA.GetProperty("spendWeight").GetProperty("score").GetDecimal());
        Assert.Equal(15m, componentsA.GetProperty("timeUrgency").GetProperty("score").GetDecimal());
        Assert.Equal(10m, componentsA.GetProperty("benchmarkOpportunity").GetProperty("score").GetDecimal());
        Assert.Equal(0m, componentsA.GetProperty("priceIncreaseRisk").GetProperty("score").GetDecimal());
        Assert.Equal(15m, componentsA.GetProperty("contractRisk").GetProperty("score").GetDecimal());
        Assert.Equal(56m, priorityABody.GetProperty("totalScore").GetDecimal());

        // AC-2 "do not invent dates" holds at the priority layer too: C's unknown renewal date
        // never fabricates a time-urgency score — it honestly defaults to the minimum.
        var priorityCResponse = await R1EndToEndTests.GetAsync(
            client, $"/api/renewals/{contractC.Id.Value}/priority", tenantId.Value);
        Assert.Equal(HttpStatusCode.OK, priorityCResponse.StatusCode);
        var priorityCBody = await R1EndToEndTests.ParseAsync(priorityCResponse);
        Assert.Equal(
            0m, priorityCBody.GetProperty("components").GetProperty("timeUrgency").GetProperty("score").GetDecimal());

        // ----- Threshold scheduler: "Threshold events fire" (AC-2), never fabricated -----

        using (var scope = _fixture.Services.CreateScope())
        {
            var scheduler = scope.ServiceProvider.GetRequiredService<RenewalThresholdScheduler>();
            var terms = new[]
            {
                new ContractRenewalTerms(contractA.Id, today.AddDays(90), AutoRenewal: true, CancellationNoticeDays: null),
                new ContractRenewalTerms(contractB.Id, today.AddDays(400), AutoRenewal: true, CancellationNoticeDays: null),
                new ContractRenewalTerms(contractC.Id, EndDate: null, AutoRenewal: true, CancellationNoticeDays: null),
            };

            var events = await scheduler.EvaluateThresholdsAsync(tenantId, terms);

            // Exactly one real threshold crossing (A at 90 days) — B (400) matches none of the
            // default 365/270/180/120/90/60/30 windows and C has nothing determinable at all, so
            // neither ever raises a fabricated event (Appendix C rule 10).
            var raised = Assert.Single(events);
            Assert.Equal(contractA.Id, raised.ContractId);
            Assert.Equal(RenewalMilestoneKind.RenewalDate, raised.Milestone);
            Assert.Equal(90, raised.ThresholdDays);
            Assert.Equal(today.AddDays(90), raised.MilestoneDate);
        }

        // The event above is durable and queryable (spec Appendix B), not just an in-memory return
        // value — this is the actual proof of the RLS-scope fix this task made (see the type doc
        // comment): the audit insert really committed against this fixture's RLS-enforced,
        // NOBYPASSRLS connection, not a superuser connection that would let it through regardless.
        using (var scope = _fixture.Services.CreateScope())
        {
            var auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            using var tenantScope = tenantContext.BeginScope(tenantId);

            var approaching = await auditDb.AuditEvents
                .Where(e => e.Action == RenewalApproachingEvent.EventName)
                .ToListAsync();

            var entry = Assert.Single(approaching);
            Assert.Equal(contractA.Id.Value.ToString(), entry.ResourceId);
            Assert.Equal(RenewalThresholdScheduler.SchedulerActor, entry.Actor);
        }

        // ----- POST /api/renewals/{id}/action: renewal-action, then upsert -----

        var setActionResponse = await R1EndToEndTests.PostAsync(
            client, $"/api/renewals/{contractA.Id.Value}/action", tenantId.Value,
            new { owner = "procurement@acme.example", status = "InProgress", action = "Started negotiation" });
        Assert.Equal(HttpStatusCode.OK, setActionResponse.StatusCode);
        var setActionBody = await R1EndToEndTests.ParseAsync(setActionResponse);
        Assert.Equal("InProgress", setActionBody.GetProperty("status").GetString());

        // Upsert, not a second row — and no dedicated GET route exists yet (see
        // RenewalActionService's own doc comment), so read back through the same service the host
        // resolves, the same "prove persistence really happened" shape R1EndToEndTests already
        // uses for ExtractionEvidence.
        var updateActionResponse = await R1EndToEndTests.PostAsync(
            client, $"/api/renewals/{contractA.Id.Value}/action", tenantId.Value,
            new { owner = "procurement@acme.example", status = "Completed", action = "Renewed at same terms" });
        Assert.Equal(HttpStatusCode.OK, updateActionResponse.StatusCode);

        using (var scope = _fixture.Services.CreateScope())
        {
            var actionService = scope.ServiceProvider.GetRequiredService<RenewalActionService>();
            var action = await actionService.GetActionAsync(tenantId, contractA.Id);

            Assert.NotNull(action);
            Assert.Equal(RenewalActionStatus.Completed, action!.Status);
            Assert.Equal("Renewed at same terms", action.Action);
        }
    }
}
