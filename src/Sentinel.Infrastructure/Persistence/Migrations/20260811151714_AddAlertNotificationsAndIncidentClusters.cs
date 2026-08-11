using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable
#pragma warning disable CA1861 // Generated migration index column arrays.

namespace Sentinel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertNotificationsAndIncidentClusters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alert_notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurrence_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    alert_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    service = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    environment = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    labels = table.Column<string>(type: "jsonb", nullable: false),
                    annotations = table.Column<string>(type: "jsonb", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_notifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "incident_clusters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    environment = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    embedding_model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    representative_embedding = table.Column<Vector>(type: "vector(768)", nullable: false),
                    occurrence_count = table.Column<int>(type: "integer", nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incident_clusters", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "incident_cluster_occurrences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cluster_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence_bundle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    similarity = table.Column<double>(type: "double precision", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incident_cluster_occurrences", x => x.id);
                    table.ForeignKey(
                        name: "FK_incident_cluster_occurrences_evidence_bundles_evidence_bund~",
                        column: x => x.evidence_bundle_id,
                        principalTable: "evidence_bundles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_incident_cluster_occurrences_incident_clusters_cluster_id",
                        column: x => x.cluster_id,
                        principalTable: "incident_clusters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alert_notifications_occurrence_received_at",
                table: "alert_notifications",
                columns: new[] { "occurrence_key", "received_at" });

            migrationBuilder.CreateIndex(
                name: "ix_incident_cluster_occurrences_cluster_occurred_at",
                table: "incident_cluster_occurrences",
                columns: new[] { "cluster_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ux_incident_cluster_occurrences_bundle",
                table: "incident_cluster_occurrences",
                column: "evidence_bundle_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_incident_clusters_scope",
                table: "incident_clusters",
                columns: new[] { "service", "environment", "embedding_model" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_notifications");

            migrationBuilder.DropTable(
                name: "incident_cluster_occurrences");

            migrationBuilder.DropTable(
                name: "incident_clusters");
        }
    }
}
#pragma warning restore CA1861
