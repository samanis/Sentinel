using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinel.Domain.Incidents;
using Sentinel.Domain.Investigations;

namespace Sentinel.Infrastructure.Persistence;

internal sealed class InvestigationRunConfiguration : IEntityTypeConfiguration<InvestigationRun>
{
    public void Configure(EntityTypeBuilder<InvestigationRun> builder)
    {
        builder.ToTable("investigation_runs");
        builder.HasKey(run => run.Id);
        builder.Property(run => run.Id).HasColumnName("id")
            .HasConversion(id => id.Value, value => new InvestigationRunId(value))
            .ValueGeneratedNever();
        builder.Property(run => run.IncidentId).HasColumnName("incident_id")
            .HasConversion(id => id.Value, value => new IncidentId(value));
        builder.Property(run => run.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(run => run.StartedAt).HasColumnName("started_at").HasColumnType("timestamp with time zone");
        builder.Property(run => run.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamp with time zone");
        builder.Property(run => run.Model).HasColumnName("model").HasMaxLength(Hypothesis.MaxModelLength);
        builder.Property(run => run.PromptVersion).HasColumnName("prompt_version").HasMaxLength(Hypothesis.MaxPromptVersionLength);
        builder.Property(run => run.TotalEvidenceCount).HasColumnName("total_evidence_count");
        builder.Property(run => run.ConsideredEvidenceCount).HasColumnName("considered_evidence_count");
        builder.Property(run => run.FailureReason).HasColumnName("failure_reason").HasMaxLength(InvestigationRun.MaxFailureReasonLength);
        builder.HasOne<Incident>().WithMany().HasForeignKey(run => run.IncidentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(run => new { run.IncidentId, run.StartedAt }).HasDatabaseName("ix_investigation_runs_incident_started_at");
    }
}
