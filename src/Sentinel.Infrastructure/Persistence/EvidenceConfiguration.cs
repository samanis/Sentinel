using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinel.Domain.Evidence;
using Sentinel.Domain.Incidents;

namespace Sentinel.Infrastructure.Persistence;

internal sealed class EvidenceConfiguration : IEntityTypeConfiguration<EvidenceItem>
{
    public void Configure(EntityTypeBuilder<EvidenceItem> builder)
    {
        builder.ToTable("evidence");

        builder.HasKey(evidence => evidence.Id);

        builder.Property(evidence => evidence.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new EvidenceId(value))
            .ValueGeneratedNever();

        builder.Property(evidence => evidence.IncidentId)
            .HasColumnName("incident_id")
            .HasConversion(id => id.Value, value => new IncidentId(value));

        builder.Property(evidence => evidence.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(evidence => evidence.SourceSystem)
            .HasColumnName("source_system")
            .HasMaxLength(EvidenceItem.MaxSourceSystemLength)
            .IsRequired();

        builder.Property(evidence => evidence.SourceReference)
            .HasColumnName("source_reference")
            .HasMaxLength(EvidenceItem.MaxSourceReferenceLength)
            .IsRequired();

        builder.Property(evidence => evidence.ObservedAt)
            .HasColumnName("observed_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(evidence => evidence.Summary)
            .HasColumnName("summary")
            .HasMaxLength(EvidenceItem.MaxSummaryLength)
            .IsRequired();

        builder.Property(evidence => evidence.SourceTraceId)
            .HasColumnName("source_trace_id")
            .HasMaxLength(EvidenceItem.MaxSourceTraceIdLength);

        builder.Property(evidence => evidence.SourceSpanId)
            .HasColumnName("source_span_id")
            .HasMaxLength(EvidenceItem.MaxSourceSpanIdLength);

        builder.Property(evidence => evidence.SourceService)
            .HasColumnName("source_service")
            .HasMaxLength(EvidenceItem.MaxSourceServiceLength);

        builder.Property(evidence => evidence.ContentHash)
            .HasColumnName("content_hash")
            .HasMaxLength(EvidenceItem.ContentHashLength)
            .IsFixedLength()
            .IsRequired();

        builder.Property(evidence => evidence.VerificationStatus)
            .HasColumnName("verification_status")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(evidence => evidence.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone");

        builder.HasOne<Incident>()
            .WithMany()
            .HasForeignKey(evidence => evidence.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(evidence => new { evidence.IncidentId, evidence.ContentHash })
            .IsUnique()
            .HasDatabaseName("ux_evidence_incident_content_hash");

        builder.HasIndex(evidence => new { evidence.IncidentId, evidence.ObservedAt })
            .HasDatabaseName("ix_evidence_incident_observed_at");

        builder.HasIndex(evidence => new { evidence.IncidentId, evidence.SourceTraceId })
            .HasDatabaseName("ix_evidence_incident_source_trace_id");

        builder.HasIndex(evidence => new { evidence.SourceTraceId, evidence.SourceSpanId })
            .HasDatabaseName("ix_evidence_source_trace_span");

        builder.HasIndex(evidence => new
            {
                evidence.IncidentId,
                evidence.SourceTraceId,
                evidence.SourceSpanId
            })
            .IsUnique()
            .HasFilter("\"type\" = 'Trace' AND \"source_system\" = 'Tempo'")
            .HasDatabaseName("ux_evidence_incident_source_trace_span");
    }
}
