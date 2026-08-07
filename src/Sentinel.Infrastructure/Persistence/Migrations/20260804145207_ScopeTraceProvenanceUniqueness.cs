using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentinel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScopeTraceProvenanceUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_evidence_incident_source_trace_span",
                table: "evidence");

            migrationBuilder.CreateIndex(
                name: "ux_evidence_incident_source_trace_span",
                table: "evidence",
                columns: ["incident_id", "source_trace_id", "source_span_id"],
                unique: true,
                filter: "\"type\" = 'Trace' AND \"source_system\" = 'Tempo'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_evidence_incident_source_trace_span",
                table: "evidence");

            migrationBuilder.CreateIndex(
                name: "ux_evidence_incident_source_trace_span",
                table: "evidence",
                columns: ["incident_id", "source_trace_id", "source_span_id"],
                unique: true);
        }
    }
}
