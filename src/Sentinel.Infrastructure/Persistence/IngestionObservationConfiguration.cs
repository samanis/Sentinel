using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinel.Domain.Ingestion;

namespace Sentinel.Infrastructure.Persistence;

internal sealed class IngestionObservationConfiguration : IEntityTypeConfiguration<IngestionObservation>
{
    public void Configure(EntityTypeBuilder<IngestionObservation> builder)
    {
        builder.ToTable("ingestion_observations");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.IngestionRunId).HasColumnName("ingestion_run_id")
            .HasConversion(id => id.Value, value => new IngestionRunId(value));
        builder.Property(item => item.SourceSystem).HasColumnName("source_system").HasMaxLength(20);
        builder.Property(item => item.SourceReference).HasColumnName("source_reference").HasMaxLength(500);
        builder.Property(item => item.ObservedAt).HasColumnName("observed_at").HasColumnType("timestamp with time zone");
        builder.Property(item => item.Summary).HasColumnName("summary").HasMaxLength(2_000);
        builder.Property(item => item.TraceId).HasColumnName("trace_id").HasMaxLength(64);
        builder.Property(item => item.SpanId).HasColumnName("span_id").HasMaxLength(64);
        builder.Property(item => item.Service).HasColumnName("service").HasMaxLength(100);
        builder.Property(item => item.ContentHash).HasColumnName("content_hash").HasMaxLength(64);
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.HasOne<IngestionRun>().WithMany()
            .HasForeignKey(item => item.IngestionRunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => new { item.IngestionRunId, item.ContentHash }).IsUnique()
            .HasDatabaseName("ux_ingestion_observations_run_content_hash");
        builder.HasIndex(item => new { item.IngestionRunId, item.SourceSystem })
            .HasDatabaseName("ix_ingestion_observations_run_source");
        builder.HasIndex(item => item.TraceId)
            .HasDatabaseName("ix_ingestion_observations_trace_id");
    }
}
