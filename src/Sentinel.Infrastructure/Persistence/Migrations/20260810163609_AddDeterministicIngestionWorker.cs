using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentinel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeterministicIngestionWorker : Migration
    {
        private static readonly string[] RunSourceColumns = ["ingestion_run_id", "source_system"];
        private static readonly string[] RunContentHashColumns = ["ingestion_run_id", "content_hash"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "log_count",
                table: "ingestion_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "loki_status",
                table: "ingestion_runs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<int>(
                name: "observation_count",
                table: "ingestion_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "tempo_status",
                table: "ingestion_runs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<int>(
                name: "trace_count",
                table: "ingestion_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "window_end",
                table: "ingestion_runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "window_start",
                table: "ingestion_runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ingestion_observations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ingestion_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_system = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    trace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    span_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    service = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_observations", x => x.id);
                    table.ForeignKey(
                        name: "FK_ingestion_observations_ingestion_runs_ingestion_run_id",
                        column: x => x.ingestion_run_id,
                        principalTable: "ingestion_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ingestion_observations_run_source",
                table: "ingestion_observations",
                columns: RunSourceColumns);

            migrationBuilder.CreateIndex(
                name: "ix_ingestion_observations_trace_id",
                table: "ingestion_observations",
                column: "trace_id");

            migrationBuilder.CreateIndex(
                name: "ux_ingestion_observations_run_content_hash",
                table: "ingestion_observations",
                columns: RunContentHashColumns,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ingestion_observations");

            migrationBuilder.DropColumn(
                name: "log_count",
                table: "ingestion_runs");

            migrationBuilder.DropColumn(
                name: "loki_status",
                table: "ingestion_runs");

            migrationBuilder.DropColumn(
                name: "observation_count",
                table: "ingestion_runs");

            migrationBuilder.DropColumn(
                name: "tempo_status",
                table: "ingestion_runs");

            migrationBuilder.DropColumn(
                name: "trace_count",
                table: "ingestion_runs");

            migrationBuilder.DropColumn(
                name: "window_end",
                table: "ingestion_runs");

            migrationBuilder.DropColumn(
                name: "window_start",
                table: "ingestion_runs");
        }
    }
}
