using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinel.Domain.Ingestion;

namespace Sentinel.Infrastructure.Persistence;

internal sealed class AlertOccurrenceConfiguration : IEntityTypeConfiguration<AlertOccurrence>
{
    public void Configure(EntityTypeBuilder<AlertOccurrence> builder)
    {
        builder.ToTable("alert_occurrences");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id")
            .HasConversion(id => id.Value, value => new AlertOccurrenceId(value))
            .ValueGeneratedNever();
        builder.Property(item => item.OccurrenceKey).HasColumnName("occurrence_key")
            .HasMaxLength(AlertOccurrence.OccurrenceKeyLength).IsRequired();
        builder.Property(item => item.AlertName).HasColumnName("alert_name")
            .HasMaxLength(AlertOccurrence.MaxAlertNameLength).IsRequired();
        builder.Property(item => item.Service).HasColumnName("service")
            .HasMaxLength(AlertOccurrence.MaxServiceLength).IsRequired();
        builder.Property(item => item.Environment).HasColumnName("environment")
            .HasMaxLength(AlertOccurrence.MaxEnvironmentLength).IsRequired();
        builder.Property(item => item.StartedAt).HasColumnName("started_at").HasColumnType("timestamp with time zone");
        builder.Property(item => item.EndsAt).HasColumnName("ends_at").HasColumnType("timestamp with time zone");
        builder.Property(item => item.ReceivedAt).HasColumnName("received_at").HasColumnType("timestamp with time zone");
        builder.Property(item => item.LabelsJson).HasColumnName("labels").HasColumnType("jsonb").IsRequired();
        builder.Property(item => item.AnnotationsJson).HasColumnName("annotations").HasColumnType("jsonb").IsRequired();
        builder.Property(item => item.GeneratorUrl).HasColumnName("generator_url")
            .HasMaxLength(AlertOccurrence.MaxGeneratorUrlLength);
        builder.HasIndex(item => item.OccurrenceKey).IsUnique()
            .HasDatabaseName("ux_alert_occurrences_occurrence_key");
        builder.HasIndex(item => new { item.Service, item.StartedAt })
            .HasDatabaseName("ix_alert_occurrences_service_started_at");
    }
}
