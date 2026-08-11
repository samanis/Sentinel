using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Sentinel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UseEmbeddingGemmaVectors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bundles are derived from durable ingestion observations. Vectors with
            // different dimensions cannot be cast safely, so the worker rebuilds them.
            migrationBuilder.Sql("DELETE FROM evidence_bundles;");

            migrationBuilder.AlterColumn<Vector>(
                name: "embedding",
                table: "evidence_bundles",
                type: "vector(768)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(384)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM evidence_bundles;");

            migrationBuilder.AlterColumn<Vector>(
                name: "embedding",
                table: "evidence_bundles",
                type: "vector(384)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(768)",
                oldNullable: true);
        }
    }
}
