using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sentinel.Infrastructure.Persistence;

internal sealed class AlertNotificationConfiguration : IEntityTypeConfiguration<AlertNotificationRecord>
{
    public void Configure(EntityTypeBuilder<AlertNotificationRecord> builder)
    {
        builder.ToTable("alert_notifications");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.OccurrenceKey).HasColumnName("occurrence_key").HasMaxLength(64).IsRequired();
        builder.Property(item => item.AlertName).HasColumnName("alert_name").HasMaxLength(200).IsRequired();
        builder.Property(item => item.Service).HasColumnName("service").HasMaxLength(100).IsRequired();
        builder.Property(item => item.Environment).HasColumnName("environment").HasMaxLength(100).IsRequired();
        builder.Property(item => item.LabelsJson).HasColumnName("labels").HasColumnType("jsonb").IsRequired();
        builder.Property(item => item.AnnotationsJson).HasColumnName("annotations").HasColumnType("jsonb").IsRequired();
        builder.Property(item => item.ReceivedAt).HasColumnName("received_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(item => new { item.OccurrenceKey, item.ReceivedAt })
            .HasDatabaseName("ix_alert_notifications_occurrence_received_at");
    }
}
