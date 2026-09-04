using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Contigo.Documents.Contracts.Migrations
{
    /// <summary>
    /// Task E02/F01/US02/T01 (us-02-staged-extraction, AC-2 "every extracted fact carries source
    /// span + confidence"): adds <c>extraction_evidence</c> for the scalar <c>contract</c> fields
    /// that have no "one row = one fact" table of their own (see
    /// <c>Domain.ExtractionEvidence</c>'s doc comment). <c>extraction_evidence</c> is a new
    /// tenant-scoped table, so — exactly like <c>AddContractLineItem</c> before it — its RLS
    /// enable/force/policy statements are bundled into this same migration rather than a separate
    /// follow-up, so there is no migration history state where it exists without RLS (ADR-009).
    /// <c>Contigo.Tenancy.Tests.TenantRlsMigrationCheckTests</c>/<c>TenantRlsDeployableScriptCheckTests</c>
    /// discover it automatically (every <c>TenantScopedEntity</c> subclass) and would fail the
    /// build if this were omitted.
    ///
    /// Does <em>not</em> also add source-span/page/confidence evidence columns to the pre-existing
    /// <c>risk</c>/<c>obligation</c>/<c>contract_line_item</c> tables, even though this task's own
    /// domain-model change (<c>Domain.Risk</c>/<c>Domain.Obligation</c>/
    /// <c>Domain.ContractLineItem</c>) added exactly those properties: the concurrently-developed
    /// task E02/F02/US01/T02 (contract-evidence-schema) added the identical columns to the same
    /// three tables one migration earlier, in <c>AddEvidenceConfidenceVersionColumns</c> — both
    /// tasks branched before either's change existed, so each authored its own copy independently.
    /// Fan-out merged both migration files (each is a distinct, uniquely-timestamped file, so
    /// there was nothing for the merge itself to flag), which left this migration's <c>Up()</c>
    /// re-issuing <c>AddColumn</c> for columns <c>AddEvidenceConfidenceVersionColumns</c> already
    /// created — fatal ("column already exists") against any database that applies migrations in
    /// order, i.e. every database, since EF Core never applies a later migration before an earlier
    /// pending one. The de-duplicated result the domain model and
    /// <c>DocumentsContractsDbContextModelSnapshot</c> already agree on (one <c>SourceSpan</c>/
    /// <c>SourcePage</c>/<c>Confidence</c> per entity) is unaffected — removing the duplicate
    /// <c>AddColumn</c> calls here does not change the final schema, only how many times it is
    /// (attempted to be) created.
    /// </summary>
    public partial class AddStagedExtractionEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "extraction_evidence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    extraction_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    field_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "text", nullable: true),
                    source_span = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    source_page = table.Column<int>(type: "integer", nullable: true),
                    confidence = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_extraction_evidence", x => x.id);
                    table.ForeignKey(
                        name: "fk_extraction_evidence_contract_contract_id",
                        column: x => x.contract_id,
                        principalTable: "contract",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_extraction_evidence_document_source_document_id",
                        column: x => x.source_document_id,
                        principalTable: "document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_extraction_evidence_extraction_job_extraction_job_id",
                        column: x => x.extraction_job_id,
                        principalTable: "extraction_job",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_extraction_evidence_contract_id_field_name",
                table: "extraction_evidence",
                columns: new[] { "contract_id", "field_name" });

            migrationBuilder.CreateIndex(
                name: "ix_extraction_evidence_extraction_job_id",
                table: "extraction_evidence",
                column: "extraction_job_id");

            migrationBuilder.CreateIndex(
                name: "ix_extraction_evidence_source_document_id",
                table: "extraction_evidence",
                column: "source_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_extraction_evidence_tenant_id",
                table: "extraction_evidence",
                column: "tenant_id");

            // ADR-009: extraction_evidence is tenant-scoped (TenantScopedEntity) and must never
            // ship without RLS — see this migration's own type doc comment for why this lives
            // here instead of a separate follow-up (mirrors AddContractLineItem).
            migrationBuilder.Sql(
                """
                ALTER TABLE "extraction_evidence" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "extraction_evidence" FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON "extraction_evidence"
                    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS tenant_isolation ON "extraction_evidence";
                ALTER TABLE "extraction_evidence" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "extraction_evidence" DISABLE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropTable(
                name: "extraction_evidence");
        }
    }
}
