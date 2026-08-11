using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinel.Domain.Evidence;
using Sentinel.Domain.Incidents;
using Sentinel.Domain.Investigations;

namespace Sentinel.Infrastructure.Persistence;

internal sealed class HypothesisConfiguration : IEntityTypeConfiguration<Hypothesis>
{
    public void Configure(EntityTypeBuilder<Hypothesis> builder)
    {
        builder.ToTable("hypotheses");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id")
            .HasConversion(id => id.Value, value => new HypothesisId(value)).ValueGeneratedNever();
        builder.Property(item => item.InvestigationRunId).HasColumnName("investigation_run_id")
            .HasConversion(id => id.Value, value => new InvestigationRunId(value));
        builder.Property(item => item.IncidentId).HasColumnName("incident_id")
            .HasConversion(id => id.Value, value => new IncidentId(value));
        builder.Property(item => item.Scope).HasColumnName("scope").HasConversion<string>().HasMaxLength(30);
        builder.Property(item => item.Statement).HasColumnName("statement").HasMaxLength(Hypothesis.MaxStatementLength).IsRequired();
        builder.Property(item => item.Confidence).HasColumnName("confidence").HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Reasoning).HasColumnName("reasoning").HasMaxLength(Hypothesis.MaxReasoningLength).IsRequired();
        builder.Property(item => item.Model).HasColumnName("model").HasMaxLength(Hypothesis.MaxModelLength).IsRequired();
        builder.Property(item => item.PromptVersion).HasColumnName("prompt_version").HasMaxLength(Hypothesis.MaxPromptVersionLength).IsRequired();
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.HasOne<InvestigationRun>().WithMany().HasForeignKey(item => item.InvestigationRunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Incident>().WithMany().HasForeignKey(item => item.IncidentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => item.InvestigationRunId).HasDatabaseName("ix_hypotheses_investigation_run_id");

        builder.OwnsMany(item => item.EvidenceReferences, references =>
        {
            references.ToTable("hypothesis_evidence_references");
            references.WithOwner().HasForeignKey("hypothesis_id");
            references.Property(item => item.EvidenceId).HasColumnName("evidence_id")
                .HasConversion(id => id.Value, value => new EvidenceId(value));
            references.Property(item => item.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20);
            references.HasKey("hypothesis_id", nameof(HypothesisEvidenceReference.EvidenceId));
            references.HasOne<EvidenceItem>().WithMany().HasForeignKey(item => item.EvidenceId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Navigation(item => item.EvidenceReferences).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
