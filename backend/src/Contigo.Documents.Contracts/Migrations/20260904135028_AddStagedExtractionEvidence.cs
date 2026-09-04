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
    /// This migration originally also (re)added source-span/page/confidence evidence columns on
    /// <c>risk</c>/<c>obligation</c>/<c>contract_line_item</c> — the same columns the
    /// sibling task E02/F02/US01/T02 was independently adding in
    /// <see cref="AddEvidenceConfidenceVersionColumns"/> (timestamped earlier, so it runs first).
    /// The two tasks' migrations converged on the same target columns without either seeing the
    /// other's work; the phase-barrier merge kept both files, which made every
    /// <c>Database.MigrateAsync()</c> call fail with Postgres error 42701 ("column ... already
    /// exists") from this migration re-adding what the earlier one had already added — 100% of
    /// this bounded context's Testcontainers-backed tests, not just this task's. Task
    /// E02/F03/US02/T01 removed the redundant <c>AddColumn</c>/<c>DropColumn</c> calls here
    /// (verified column-for-column against <see cref="AddEvidenceConfidenceVersionColumns"/>'s
    /// own <c>Up()</c>); the end-state schema is unchanged; only the broken duplicate add is
    /// gone. <c>Migrations/Scripts/documents-contracts.sql</c> was regenerated from this fix via
    /// `dotnet ef migrations script --idempotent` (was itself corrupted by the same merge —
    /// mismatched `IF`/`END IF` nesting from interleaving two independently-generated script
    /// halves).
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
