using System.Globalization;
using System.Text.Json;
using Contigo.Documents.Contracts.Domain;
using Contigo.Documents.Contracts.Infrastructure;
using Contigo.SharedKernel;
using Contigo.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Contigo.Documents.Contracts.Application;

/// <summary>
/// Implements task E02/F05/US01/T01 (us-01-correction-history, AC-1/AC-2/AC-3): `PATCH
/// /api/contracts/{id}` records a human correction to one or more extracted
/// <see cref="Contract"/> fields as a new, append-only <see cref="ContractVersion"/> snapshot plus
/// one <see cref="CorrectionHistory"/> row per changed field — never a silent in-place overwrite
/// with no trace (product spec Appendix C rule 5: "Never destructively overwrite contract history
/// or human corrections"; rule 9: "Capture ... corrections from day one").
///
/// AC-2 ("original AI extraction is preserved"): the first time a given <see cref="Contract"/> is
/// corrected, this service snapshots its *pre-correction* state as <see cref="ContractVersion"/>
/// #1 before anything below mutates it — this module has no separate extraction-time writer yet
/// (that lands with the extraction pipeline feature), so the row as it stands the first time a
/// correction reaches it *is* the original extraction. Every subsequent correction — on this
/// contract or a later one — only ever appends version N+1; no <see cref="ContractVersion"/> row
/// is ever mutated or deleted once written.
///
/// Same interim-actor placeholder as <c>DocumentUploadService.UnattributedActor</c> (see that
/// type's own doc comment): ADR-010 is not in this task's "Architecture decisions in force" list,
/// so there is no validated caller identity to record on
/// <see cref="CorrectionHistory.CorrectedBy"/>/<see cref="ContractVersion.CreatedBy"/> yet, and a
/// client-supplied "correctedBy" on the request body would be an unverified, spoofable identity —
/// worse than an explicit, honest placeholder.
///
/// Owns its own tenant scope (<see cref="ITenantContext.BeginScope"/>), same rationale as every
/// other Application-layer service in this module (ADR-009 belt-and-suspenders: the explicit
/// <c>tenant_id</c> predicate below plus Postgres RLS are two independent reasons a cross-tenant
/// contract id reads back as "not found", not one).
///
/// Task E02/F05/US01/T02 (correction-audit) added the <see cref="IAuditWriter"/> dependency:
/// every successful correction also writes one append-only <see cref="AuditEntry"/> (same
/// gateway-abstraction shape <see cref="DocumentUploadService"/> already uses for its own
/// "upload -> audit event" write — <see cref="IAuditWriter"/> lives in
/// <c>Contigo.SharedKernel</c>, not <c>Contigo.Audit</c>, so this does not cross the ADR-002
/// module boundary). This is a distinct read surface from <see cref="CorrectionHistory"/> itself:
/// the audit trail (<c>GET /api/audit</c>) answers "who changed something, when, on which
/// resource" across every module, while <see cref="CorrectionHistory"/> /
/// <c>ContractCorrectionHistoryQueryService</c> answers "what exactly changed on this contract,
/// field by field" — see <c>Contigo.Audit.Domain.AuditEvent</c>'s own doc comment on why the two
/// are allowed to diverge rather than one subsuming the other.
/// </summary>
public sealed class ContractCorrectionService(
    DocumentsContractsDbContext dbContext, ITenantContext tenantContext, IClock clock, IAuditWriter auditWriter)
{
    /// <summary>Returned by <see cref="CorrectAsync"/> when no contract with the given id exists
    /// for the caller's tenant. <c>Contigo.Api.ContractsEndpointExtensions</c> maps exactly this
    /// string to 404; every other failure maps to 400.</summary>
    public const string ContractNotFoundError = "Contract not found.";

    private const int InitialVersionNumber = 1;
    private const string ContractEntityType = nameof(Contract);
    private const string OriginalExtractionChangeReason = "Original extraction (pre-correction baseline).";

    /// <summary><see cref="AuditEntry.Action"/> for every correction (task E02/F05/US01/T02).
    /// Past-tense, matching this codebase's established convention
    /// (<see cref="DocumentUploadService"/>'s <c>"document.uploaded"</c>,
    /// <c>Contigo.AiGateway.Logging.LoggingAiGateway</c>'s <c>"ai.{role}"</c> role names) rather
    /// than <c>Contigo.Audit.Domain.AuditEvent.Action</c>'s own doc-comment example
    /// (<c>"contract.correction"</c>), which is illustrative, not a pinned literal.</summary>
    private const string AuditCorrectedAction = "contract.corrected";

    /// <summary><see cref="AuditEntry.ResourceType"/> for every correction — lowercase, matching
    /// <see cref="DocumentUploadService"/>'s own <c>"document"</c> (not
    /// <see cref="ContractEntityType"/>'s PascalCase, which is this module's own
    /// <see cref="CorrectionHistory.TargetEntityType"/> discriminator, a separate convention).</summary>
    private const string AuditResourceType = "contract";

    /// <summary>Same placeholder actor as <c>DocumentUploadService.UnattributedActor</c> — see
    /// the type doc comment above for why this task does not accept/trust a caller-supplied
    /// identity.</summary>
    private const string UnattributedActor = "unattributed";

    /// <summary>The only field names <see cref="CorrectAsync"/> accepts in its <c>corrections</c>
    /// map — every other <see cref="Contract"/> property is either an identity column
    /// (<c>Id</c>/<c>TenantId</c>), a cross-aggregate reference
    /// (<c>SupplierId</c>/<c>ParentContractId</c> — corrected by re-linking, not a text edit) or
    /// audit metadata (<c>CreatedAt</c>), not a "deterministic field" a human corrects from the
    /// review UI (product spec Appendix C rule 5/9).</summary>
    public static IReadOnlyCollection<string> CorrectableFieldNames => CorrectableFields.Keys;

    private static readonly Dictionary<string, FieldDefinition> CorrectableFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = RequiredEnum("type", c => c.Type, (c, v) => c.Type = v),
            ["status"] = RequiredText("status", c => c.Status, (c, v) => c.Status = v),
            ["currency"] = RequiredText("currency", c => c.Currency, (c, v) => c.Currency = v),
            ["startDate"] = OptionalDate("startDate", c => c.StartDate, (c, v) => c.StartDate = v),
            ["endDate"] = OptionalDate("endDate", c => c.EndDate, (c, v) => c.EndDate = v),
            ["effectiveDate"] = OptionalDate("effectiveDate", c => c.EffectiveDate, (c, v) => c.EffectiveDate = v),
            ["cancellationDeadline"] = OptionalDate(
                "cancellationDeadline", c => c.CancellationDeadline, (c, v) => c.CancellationDeadline = v),
            ["annualSpend"] = OptionalDecimal("annualSpend", c => c.AnnualSpend, (c, v) => c.AnnualSpend = v),
            ["totalContractValue"] = OptionalDecimal(
                "totalContractValue", c => c.TotalContractValue, (c, v) => c.TotalContractValue = v),
            ["autoRenewal"] = RequiredBool("autoRenewal", c => c.AutoRenewal, (c, v) => c.AutoRenewal = v),
            ["renewalTermMonths"] = OptionalInt(
                "renewalTermMonths", c => c.RenewalTermMonths, (c, v) => c.RenewalTermMonths = v),
            ["paymentTerms"] = OptionalText(c => c.PaymentTerms, (c, v) => c.PaymentTerms = v),
            ["governingLaw"] = OptionalText(c => c.GoverningLaw, (c, v) => c.GoverningLaw = v),
        };

    public async Task<Result<ContractCorrectionResult>> CorrectAsync(
        TenantId tenantId,
        EntityId contractId,
        IReadOnlyDictionary<string, string?> corrections,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (corrections.Count == 0)
        {
            return Result<ContractCorrectionResult>.Failure("At least one field correction is required.");
        }

        var unknownFields = corrections.Keys.Where(name => !CorrectableFields.ContainsKey(name)).ToList();
        if (unknownFields.Count > 0)
        {
            return Result<ContractCorrectionResult>.Failure(
                $"Unknown or non-correctable field(s): {string.Join(", ", unknownFields)}. " +
                $"Correctable fields: {string.Join(", ", CorrectableFields.Keys)}.");
        }

        using var tenantScope = tenantContext.BeginScope(tenantId);

        var contract = await dbContext.Contracts
            .SingleOrDefaultAsync(c => c.TenantId == tenantId && c.Id == contractId, cancellationToken)
            .ConfigureAwait(false);

        if (contract is null)
        {
            return Result<ContractCorrectionResult>.Failure(ContractNotFoundError);
        }

        // Phase 1 — validate/parse every requested value up front, without mutating `contract` or
        // staging a single write. This DbContext instance is request/job-scoped and may be reused
        // by its caller for a later operation (a batch of corrections, a retry, a test); one bad
        // value in a multi-field PATCH must fail the whole request with zero trace, not leave a
        // half-applied mutation or a stray tracked entity for that later call's SaveChanges to
        // accidentally pick up.
        var previousValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var canonicalNewValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (fieldName, rawValue) in corrections)
        {
            var validated = CorrectableFields[fieldName].Validate(rawValue);
            if (validated.IsFailure)
            {
                return Result<ContractCorrectionResult>.Failure(validated.Error);
            }

            previousValues[fieldName] = CorrectableFields[fieldName].Read(contract);
            canonicalNewValues[fieldName] = validated.Value;
        }

        var latestVersionNumber = await dbContext.ContractVersions
            .Where(v => v.TenantId == tenantId && v.ContractId == contractId)
            .Select(v => (int?)v.VersionNumber)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false);

        // AC-2: if this contract has never been versioned, its current — still pre-mutation at
        // this point — state is the original extraction. Captured as a plain string now, not yet
        // staged with dbContext.Add, so a no-op short-circuit below still leaves the DbContext
        // untouched (see the Phase 1 comment).
        var originalExtractionSnapshotJson = latestVersionNumber is null ? Snapshot(contract) : null;

        var now = clock.UtcNow;

        // Phase 2 — every value already validated; mutate `contract` only for fields that
        // actually change, and stage a CorrectionHistory row 1:1 with each mutation.
        var correctedFields = new List<string>();
        foreach (var fieldName in corrections.Keys)
        {
            var previousValue = previousValues[fieldName];
            var newValue = canonicalNewValues[fieldName];
            if (string.Equals(previousValue, newValue, StringComparison.Ordinal))
            {
                continue; // No actual change — do not fabricate a history row for a no-op.
            }

            CorrectableFields[fieldName].Write(contract, newValue);
            dbContext.CorrectionHistories.Add(new CorrectionHistory
            {
                TenantId = tenantId,
                TargetEntityType = ContractEntityType,
                TargetEntityId = contractId,
                FieldName = fieldName,
                PreviousValue = previousValue,
                NewValue = newValue,
                CorrectedBy = UnattributedActor,
                CorrectedAt = now,
                Reason = reason,
            });
            correctedFields.Add(fieldName);
        }

        if (correctedFields.Count == 0)
        {
            // Nothing was mutated and nothing was Add()ed above (every field hit the `continue`
            // branch) — including the version-1 baseline, which is only staged below, after this
            // check. A request that changes nothing has zero side effects, even for a contract
            // that has never been versioned.
            return Result<ContractCorrectionResult>.Failure(
                "None of the supplied values differ from the contract's current values.");
        }

        if (latestVersionNumber is null)
        {
            dbContext.ContractVersions.Add(new ContractVersion
            {
                TenantId = tenantId,
                ContractId = contractId,
                VersionNumber = InitialVersionNumber,
                SnapshotJson = originalExtractionSnapshotJson!,
                ChangeReason = OriginalExtractionChangeReason,
                CreatedBy = UnattributedActor,
                CreatedAt = contract.CreatedAt,
            });
            latestVersionNumber = InitialVersionNumber;
        }

        var newVersionNumber = latestVersionNumber.Value + 1;
        dbContext.ContractVersions.Add(new ContractVersion
        {
            TenantId = tenantId,
            ContractId = contractId,
            VersionNumber = newVersionNumber,
            SnapshotJson = Snapshot(contract),
            ChangeReason = reason,
            CreatedBy = UnattributedActor,
            CreatedAt = now,
        });

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Task E02/F05/US01/T02 (correction-audit): recorded only once the correction itself is
        // durable, still inside this call's own tenant scope (see the type doc comment), same
        // placement as DocumentUploadService.UploadAsync's own "upload -> audit event" write. A
        // failure here throws and fails the whole request rather than silently dropping the audit
        // record — ADR-011 treats audit as a compliance control, not a best-effort side-channel.
        await auditWriter.WriteAsync(
            new AuditEntry(
                tenantId,
                UnattributedActor,
                AuditCorrectedAction,
                AuditResourceType,
                contract.Id.Value.ToString(),
                now,
                BuildAuditDetail(newVersionNumber, correctedFields, reason)),
            cancellationToken).ConfigureAwait(false);

        return Result<ContractCorrectionResult>.Success(
            new ContractCorrectionResult(contract.Id, newVersionNumber, correctedFields, now));
    }

    /// <summary><see cref="AuditEntry.Detail"/> for a correction: the resulting version number
    /// and which fields changed, plus the caller's own free-text reason when supplied. Recording
    /// <paramref name="reason"/> here is not a new exposure — it is already stored, verbatim, on
    /// every affected <see cref="CorrectionHistory.Reason"/> row written just above. Space-separated
    /// key=value pairs, same convention as <c>Contigo.AiGateway.Logging.LoggingAiGateway.LogAsync</c>'s
    /// own <c>Detail</c> string.</summary>
    private static string BuildAuditDetail(int versionNumber, IReadOnlyList<string> correctedFields, string? reason)
    {
        var detail = $"versionNumber={versionNumber} correctedFields={string.Join(",", correctedFields)}";
        return reason is null ? detail : $"{detail} reason={reason}";
    }

    /// <summary>Point-in-time JSON snapshot of every material <see cref="Contract"/> field,
    /// written verbatim into <see cref="ContractVersion.SnapshotJson"/> (a <c>jsonb</c> column —
    /// ADR-003: "history survives schema growth without a migration per new snapshotted
    /// field").</summary>
    private static string Snapshot(Contract contract) => JsonSerializer.Serialize(new
    {
        contract.SupplierId,
        contract.ParentContractId,
        Type = contract.Type.ToString(),
        contract.Status,
        contract.Currency,
        contract.StartDate,
        contract.EndDate,
        contract.EffectiveDate,
        contract.CancellationDeadline,
        contract.AnnualSpend,
        contract.TotalContractValue,
        contract.AutoRenewal,
        contract.RenewalTermMonths,
        contract.PaymentTerms,
        contract.GoverningLaw,
    });

    // ------- field table: one entry per correctable Contract property -------

    private sealed record FieldDefinition(
        Func<Contract, string?> Read,
        Func<string?, Result<string?>> Validate,
        Action<Contract, string?> Write);

    private static FieldDefinition RequiredText(string fieldName, Func<Contract, string> read, Action<Contract, string> write) =>
        new(
            Read: c => read(c),
            Validate: raw => string.IsNullOrWhiteSpace(raw)
                ? Result<string?>.Failure($"'{fieldName}' cannot be cleared; supply a non-empty value.")
                : Result<string?>.Success(raw),
            Write: (c, value) => write(c, value!));

    private static FieldDefinition OptionalText(Func<Contract, string?> read, Action<Contract, string?> write) =>
        new(
            Read: read,
            Validate: raw => Result<string?>.Success(raw),
            Write: write);

    private static FieldDefinition OptionalDate(string fieldName, Func<Contract, DateOnly?> read, Action<Contract, DateOnly?> write) =>
        new(
            Read: c => read(c)?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Validate: raw =>
            {
                if (raw is null)
                {
                    return Result<string?>.Success(null);
                }

                if (!DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                {
                    return Result<string?>.Failure($"'{fieldName}' must be an ISO date (yyyy-MM-dd).");
                }

                return Result<string?>.Success(parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            },
            Write: (c, value) => write(
                c, value is null ? null : DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture)));

    private static FieldDefinition OptionalDecimal(string fieldName, Func<Contract, decimal?> read, Action<Contract, decimal?> write) =>
        new(
            Read: c => read(c)?.ToString(CultureInfo.InvariantCulture),
            Validate: raw =>
            {
                if (raw is null)
                {
                    return Result<string?>.Success(null);
                }

                if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                {
                    return Result<string?>.Failure($"'{fieldName}' must be a decimal number.");
                }

                return Result<string?>.Success(parsed.ToString(CultureInfo.InvariantCulture));
            },
            Write: (c, value) => write(
                c, value is null ? null : decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture)));

    private static FieldDefinition OptionalInt(string fieldName, Func<Contract, int?> read, Action<Contract, int?> write) =>
        new(
            Read: c => read(c)?.ToString(CultureInfo.InvariantCulture),
            Validate: raw =>
            {
                if (raw is null)
                {
                    return Result<string?>.Success(null);
                }

                if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    return Result<string?>.Failure($"'{fieldName}' must be a whole number.");
                }

                return Result<string?>.Success(parsed.ToString(CultureInfo.InvariantCulture));
            },
            Write: (c, value) => write(
                c, value is null ? null : int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)));

    private static FieldDefinition RequiredBool(string fieldName, Func<Contract, bool> read, Action<Contract, bool> write) =>
        new(
            Read: c => read(c) ? "true" : "false",
            Validate: raw => raw is not null && bool.TryParse(raw, out var parsed)
                ? Result<string?>.Success(parsed ? "true" : "false")
                : Result<string?>.Failure($"'{fieldName}' must be 'true' or 'false'."),
            Write: (c, value) => write(c, value == "true"));

    private static FieldDefinition RequiredEnum(
        string fieldName, Func<Contract, ContractDocumentType> read, Action<Contract, ContractDocumentType> write) =>
        new(
            Read: c => read(c).ToString(),
            Validate: raw => raw is not null && Enum.TryParse<ContractDocumentType>(raw, ignoreCase: true, out var parsed)
                ? Result<string?>.Success(parsed.ToString())
                : Result<string?>.Failure(
                    $"'{fieldName}' must be one of: {string.Join(", ", Enum.GetNames<ContractDocumentType>())}."),
            Write: (c, value) => write(c, Enum.Parse<ContractDocumentType>(value!)));
}
