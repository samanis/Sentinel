using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentinel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertIngestionInbox : Migration
    {
        private static readonly string[] AlertServiceStartColumns = ["service", "started_at"];
        private static readonly string[] IngestionStatusCreatedColumns = ["status", "created_at"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alert_occurrences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurrence_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    alert_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    service = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    environment = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    labels = table.Column<string>(type: "jsonb", nullable: false),
                    annotations = table.Column<string>(type: "jsonb", nullable: false),
                    generator_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_occurrences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ingestion_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    alert_occurrence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_runs", x => x.id);
                    table.ForeignKey(
                        name: "FK_ingestion_runs_alert_occurrences_alert_occurrence_id",
                        column: x => x.alert_occurrence_id,
                        principalTable: "alert_occurrences",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alert_occurrences_service_started_at",
                table: "alert_occurrences",
                columns: AlertServiceStartColumns);

            migrationBuilder.CreateIndex(
                name: "ux_alert_occurrences_occurrence_key",
                table: "alert_occurrences",
                column: "occurrence_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ingestion_runs_status_created_at",
                table: "ingestion_runs",
                columns: IngestionStatusCreatedColumns);

            migrationBuilder.CreateIndex(
                name: "ux_ingestion_runs_alert_occurrence_id",
                table: "ingestion_runs",
                column: "alert_occurrence_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ingestion_runs");

            migrationBuilder.DropTable(
                name: "alert_occurrences");
        }
    }
}
