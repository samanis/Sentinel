using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinel.Domain.Incidents;

namespace Sentinel.Infrastructure.Persistence;

internal sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("incidents");

        builder.HasKey(incident => incident.Id);

        builder.Property(incident => incident.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => new IncidentId(value))
            .ValueGeneratedNever();

        builder.Property(incident => incident.Title)
            .HasColumnName("title")
            .HasMaxLength(Incident.MaxTitleLength)
            .IsRequired();

        builder.Property(incident => incident.Service)
            .HasColumnName("service")
            .HasMaxLength(Incident.MaxServiceLength)
            .IsRequired();

        builder.Property(incident => incident.StartedAt)
            .HasColumnName("started_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(incident => incident.Severity)
            .HasColumnName("severity")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(incident => incident.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(incident => incident.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(incident => incident.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(incident => incident.ResolvedAt)
            .HasColumnName("resolved_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(incident => incident.ClosedAt)
            .HasColumnName("closed_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(incident => incident.CreatedAt)
            .HasDatabaseName("ix_incidents_created_at");

        builder.HasIndex(incident => new { incident.Service, incident.Status })
            .HasDatabaseName("ix_incidents_service_status");
    }
}
