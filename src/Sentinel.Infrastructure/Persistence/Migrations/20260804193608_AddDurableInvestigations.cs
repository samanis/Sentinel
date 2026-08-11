using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Sentinel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableInvestigations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "investigation_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    prompt_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    total_evidence_count = table.Column<int>(type: "integer", nullable: false),
                    considered_evidence_count = table.Column<int>(type: "integer", nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investigation_runs", x => x.id);
                    table.ForeignKey(
                        name: "FK_investigation_runs_incidents_incident_id",
                        column: x => x.incident_id,
                        principalTable: "incidents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "evidence_relationships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    investigation_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    strength = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    explanation = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    prompt_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence_relationships", x => x.id);
                    table.ForeignKey(
                        name: "FK_evidence_relationships_evidence_source_evidence_id",
                        column: x => x.source_evidence_id,
                        principalTable: "evidence",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evidence_relationships_evidence_target_evidence_id",
                        column: x => x.target_evidence_id,
                        principalTable: "evidence",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evidence_relationships_incidents_incident_id",
                        column: x => x.incident_id,
                        principalTable: "incidents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_evidence_relationships_investigation_runs_investigation_run~",
                        column: x => x.investigation_run_id,
                        principalTable: "investigation_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "hypotheses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    investigation_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    statement = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    confidence = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reasoning = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    prompt_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hypotheses", x => x.id);
                    table.ForeignKey(
                        name: "FK_hypotheses_incidents_incident_id",
                        column: x => x.incident_id,
                        principalTable: "incidents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_hypotheses_investigation_runs_investigation_run_id",
                        column: x => x.investigation_run_id,
                        principalTable: "investigation_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "hypothesis_evidence_references",
                columns: table => new
                {
                    evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hypothesis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hypothesis_evidence_references", x => new { x.hypothesis_id, x.evidence_id });
                    table.ForeignKey(
                        name: "FK_hypothesis_evidence_references_evidence_evidence_id",
                        column: x => x.evidence_id,
                        principalTable: "evidence",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hypothesis_evidence_references_hypotheses_hypothesis_id",
                        column: x => x.hypothesis_id,
                        principalTable: "hypotheses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_relationships_incident_id",
                table: "evidence_relationships",
                column: "incident_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_relationships_investigation_run_id",
                table: "evidence_relationships",
                column: "investigation_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_relationships_source_evidence_id",
                table: "evidence_relationships",
                column: "source_evidence_id");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_relationships_target_evidence_id",
                table: "evidence_relationships",
                column: "target_evidence_id");

            migrationBuilder.CreateIndex(
                name: "IX_hypotheses_incident_id",
                table: "hypotheses",
                column: "incident_id");

            migrationBuilder.CreateIndex(
                name: "ix_hypotheses_investigation_run_id",
                table: "hypotheses",
                column: "investigation_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_hypothesis_evidence_references_evidence_id",
                table: "hypothesis_evidence_references",
                column: "evidence_id");

            migrationBuilder.CreateIndex(
                name: "ix_investigation_runs_incident_started_at",
                table: "investigation_runs",
                columns: new[] { "incident_id", "started_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evidence_relationships");

            migrationBuilder.DropTable(
                name: "hypothesis_evidence_references");

            migrationBuilder.DropTable(
                name: "hypotheses");

            migrationBuilder.DropTable(
                name: "investigation_runs");
        }
    }
}
