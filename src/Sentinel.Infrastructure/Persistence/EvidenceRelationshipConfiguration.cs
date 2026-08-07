using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinel.Domain.Evidence;
using Sentinel.Domain.Incidents;
using Sentinel.Domain.Investigations;

namespace Sentinel.Infrastructure.Persistence;

internal sealed class EvidenceRelationshipConfiguration : IEntityTypeConfiguration<EvidenceRelationship>
{
    public void Configure(EntityTypeBuilder<EvidenceRelationship> builder)
    {
        builder.ToTable("evidence_relationships");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id")
            .HasConversion(id => id.Value, value => new EvidenceRelationshipId(value)).ValueGeneratedNever();
        builder.Property(item => item.InvestigationRunId).HasColumnName("investigation_run_id")
            .HasConversion(id => id.Value, value => new InvestigationRunId(value));
        builder.Property(item => item.IncidentId).HasColumnName("incident_id")
            .HasConversion(id => id.Value, value => new IncidentId(value));
        builder.Property(item => item.SourceEvidenceId).HasColumnName("source_evidence_id")
            .HasConversion(id => id.Value, value => new EvidenceId(value));
        builder.Property(item => item.TargetEvidenceId).HasColumnName("target_evidence_id")
            .HasConversion(id => id.Value, value => new EvidenceId(value));
        builder.Property(item => item.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(30);
        builder.Property(item => item.Strength).HasColumnName("strength").HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Explanation).HasColumnName("explanation").HasMaxLength(EvidenceRelationship.MaxExplanationLength).IsRequired();
        builder.Property(item => item.Model).HasColumnName("model").HasMaxLength(EvidenceRelationship.MaxModelLength).IsRequired();
        builder.Property(item => item.PromptVersion).HasColumnName("prompt_version").HasMaxLength(EvidenceRelationship.MaxPromptVersionLength).IsRequired();
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.HasOne<InvestigationRun>().WithMany().HasForeignKey(item => item.InvestigationRunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Incident>().WithMany().HasForeignKey(item => item.IncidentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<EvidenceItem>().WithMany().HasForeignKey(item => item.SourceEvidenceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EvidenceItem>().WithMany().HasForeignKey(item => item.TargetEvidenceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.InvestigationRunId).HasDatabaseName("ix_evidence_relationships_investigation_run_id");
    }
}
