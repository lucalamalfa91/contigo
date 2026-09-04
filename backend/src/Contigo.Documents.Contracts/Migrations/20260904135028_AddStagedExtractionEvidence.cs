using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Contigo.Documents.Contracts.Migrations
{
    /// <summary>
    /// Task E02/F01/US02/T01 (us-02-staged-extraction, AC-2 "every extracted fact carries source
    /// span + confidence"): completes source-span/page/confidence evidence on
    /// <c>risk</c>/<c>obligation</c>/<c>contract_line_item</c> (already-existing "one row = one
    /// fact" tables that were missing one or more of those columns — see
    /// <c>Domain.Risk</c>/<c>Domain.Obligation</c>/<c>Domain.ContractLineItem</c>'s own doc
    /// comments), and adds <c>extraction_evidence</c> for the scalar <c>contract</c> fields that
    /// have no "one row = one fact" table of their own (see <c>Domain.ExtractionEvidence</c>'s
    /// doc comment). <c>extraction_evidence</c> is a new tenant-scoped table, so — exactly like
    /// <c>AddContractLineItem</c> before it — its RLS enable/force/policy statements are bundled
    /// into this same migration rather than a separate follow-up, so there is no migration
    /// history state where it exists without RLS (ADR-009).
    /// <c>Contigo.Tenancy.Tests.TenantRlsMigrationCheckTests</c>/<c>TenantRlsDeployableScriptCheckTests</c>
    /// discover it automatically (every <c>TenantScopedEntity</c> subclass) and would fail the
    /// build if this were omitted.
    /// </summary>
    public partial class AddStagedExtractionEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "source_page",
                table: "risk",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_span",
                table: "risk",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source_page",
                table: "obligation",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_span",
                table: "obligation",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "confidence",
                table: "contract_line_item",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source_page",
                table: "contract_line_item",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_span",
                table: "contract_line_item",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

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

            migrationBuilder.DropColumn(
                name: "source_page",
                table: "risk");

            migrationBuilder.DropColumn(
                name: "source_span",
                table: "risk");

            migrationBuilder.DropColumn(
                name: "source_page",
                table: "obligation");

            migrationBuilder.DropColumn(
                name: "source_span",
                table: "obligation");

            migrationBuilder.DropColumn(
                name: "confidence",
                table: "contract_line_item");

            migrationBuilder.DropColumn(
                name: "source_page",
                table: "contract_line_item");

            migrationBuilder.DropColumn(
                name: "source_span",
                table: "contract_line_item");
        }
    }
}
