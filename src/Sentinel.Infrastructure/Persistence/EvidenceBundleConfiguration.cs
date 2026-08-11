using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinel.Domain.Ingestion;
using Sentinel.Infrastructure.AI;

namespace Sentinel.Infrastructure.Persistence;

internal sealed class EvidenceBundleConfiguration : IEntityTypeConfiguration<EvidenceBundleRecord>
{
    public void Configure(EntityTypeBuilder<EvidenceBundleRecord> builder)
    {
        builder.ToTable("evidence_bundles");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.IngestionRunId).HasColumnName("ingestion_run_id")
            .HasConversion(id => id.Value, value => new IngestionRunId(value));
        builder.Property(item => item.AlertName).HasColumnName("alert_name").HasMaxLength(200);
        builder.Property(item => item.Service).HasColumnName("service").HasMaxLength(100);
        builder.Property(item => item.Environment).HasColumnName("environment").HasMaxLength(100);
        builder.Property(item => item.Status).HasColumnName("status").HasMaxLength(20);
        builder.Property(item => item.SearchDocument).HasColumnName("search_document").HasColumnType("text");
        builder.Property(item => item.Embedding).HasColumnName("embedding")
            .HasColumnType($"vector({EmbeddingOptions.RequiredDimensions})");
        builder.Property(item => item.EmbeddingModel).HasColumnName("embedding_model").HasMaxLength(100);
        builder.Property(item => item.EmbeddingDimensions).HasColumnName("embedding_dimensions");
        builder.Property(item => item.FailureCode).HasColumnName("failure_code").HasMaxLength(100);
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.Property(item => item.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamp with time zone");
        builder.HasOne<IngestionRun>().WithOne()
            .HasForeignKey<EvidenceBundleRecord>(item => item.IngestionRunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => item.IngestionRunId).IsUnique()
            .HasDatabaseName("ux_evidence_bundles_ingestion_run_id");
        builder.HasIndex(item => new { item.EmbeddingModel, item.Service, item.Environment })
            .HasDatabaseName("ix_evidence_bundles_search_scope");
    }
}
