using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentinel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredEvidenceProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "source_service",
                table: "evidence",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_span_id",
                table: "evidence",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_trace_id",
                table: "evidence",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE evidence
                SET source_trace_id = split_part(
                        split_part(source_reference, 'tempo://traces/', 2),
                        '/spans/',
                        1),
                    source_span_id = replace(
                        replace(
                            replace(
                                split_part(source_reference, '/spans/', 2),
                                '%2F',
                                '/'),
                            '%2B',
                            '+'),
                        '%3D',
                        '='),
                    source_service = substring(summary from '^Service ''([^'']+)''')
                WHERE source_system = 'Tempo'
                  AND source_reference LIKE 'tempo://traces/%/spans/%';
                """);

            migrationBuilder.CreateIndex(
                name: "ix_evidence_incident_source_trace_id",
                table: "evidence",
                columns: ["incident_id", "source_trace_id"]);

            migrationBuilder.CreateIndex(
                name: "ix_evidence_source_trace_span",
                table: "evidence",
                columns: ["source_trace_id", "source_span_id"]);

            migrationBuilder.CreateIndex(
                name: "ux_evidence_incident_source_trace_span",
                table: "evidence",
                columns: ["incident_id", "source_trace_id", "source_span_id"],
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_evidence_incident_source_trace_id",
                table: "evidence");

            migrationBuilder.DropIndex(
                name: "ix_evidence_source_trace_span",
                table: "evidence");

            migrationBuilder.DropIndex(
                name: "ux_evidence_incident_source_trace_span",
                table: "evidence");

            migrationBuilder.DropColumn(
                name: "source_service",
                table: "evidence");

            migrationBuilder.DropColumn(
                name: "source_span_id",
                table: "evidence");

            migrationBuilder.DropColumn(
                name: "source_trace_id",
                table: "evidence");
        }
    }
}
