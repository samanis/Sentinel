using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinel.Domain.Ingestion;

namespace Sentinel.Infrastructure.Persistence;

internal sealed class IngestionRunConfiguration : IEntityTypeConfiguration<IngestionRun>
{
    public void Configure(EntityTypeBuilder<IngestionRun> builder)
    {
        builder.ToTable("ingestion_runs");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id")
            .HasConversion(id => id.Value, value => new IngestionRunId(value))
            .ValueGeneratedNever();
        builder.Property(item => item.AlertOccurrenceId).HasColumnName("alert_occurrence_id")
            .HasConversion(id => id.Value, value => new AlertOccurrenceId(value));
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.AttemptCount).HasColumnName("attempt_count");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.Property(item => item.StartedAt).HasColumnName("started_at").HasColumnType("timestamp with time zone");
        builder.Property(item => item.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamp with time zone");
        builder.Property(item => item.FailureCode).HasColumnName("failure_code").HasMaxLength(100);
        builder.Property(item => item.WindowStart).HasColumnName("window_start").HasColumnType("timestamp with time zone");
        builder.Property(item => item.WindowEnd).HasColumnName("window_end").HasColumnType("timestamp with time zone");
        builder.Property(item => item.LokiStatus).HasColumnName("loki_status").HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.TempoStatus).HasColumnName("tempo_status").HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.LogCount).HasColumnName("log_count");
        builder.Property(item => item.TraceCount).HasColumnName("trace_count");
        builder.Property(item => item.ObservationCount).HasColumnName("observation_count");
        builder.HasOne<AlertOccurrence>().WithOne()
            .HasForeignKey<IngestionRun>(item => item.AlertOccurrenceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => item.AlertOccurrenceId).IsUnique()
            .HasDatabaseName("ux_ingestion_runs_alert_occurrence_id");
        builder.HasIndex(item => new { item.Status, item.CreatedAt })
            .HasDatabaseName("ix_ingestion_runs_status_created_at");
    }
}
