using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sentinel.Infrastructure.Persistence;

internal sealed class IncidentClusterConfiguration : IEntityTypeConfiguration<IncidentClusterRecord>
{
    public void Configure(EntityTypeBuilder<IncidentClusterRecord> builder)
    {
        builder.ToTable("incident_clusters");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.Service).HasColumnName("service").HasMaxLength(100).IsRequired();
        builder.Property(item => item.Environment).HasColumnName("environment").HasMaxLength(100).IsRequired();
        builder.Property(item => item.EmbeddingModel).HasColumnName("embedding_model").HasMaxLength(100).IsRequired();
        builder.Property(item => item.RepresentativeEmbedding).HasColumnName("representative_embedding").HasColumnType("vector(768)");
        builder.Property(item => item.OccurrenceCount).HasColumnName("occurrence_count");
        builder.Property(item => item.FirstSeenAt).HasColumnName("first_seen_at").HasColumnType("timestamp with time zone");
        builder.Property(item => item.LastSeenAt).HasColumnName("last_seen_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(item => new { item.Service, item.Environment, item.EmbeddingModel })
            .HasDatabaseName("ix_incident_clusters_scope");
    }
}

internal sealed class IncidentClusterOccurrenceConfiguration : IEntityTypeConfiguration<IncidentClusterOccurrenceRecord>
{
    public void Configure(EntityTypeBuilder<IncidentClusterOccurrenceRecord> builder)
    {
        builder.ToTable("incident_cluster_occurrences");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.ClusterId).HasColumnName("cluster_id");
        builder.Property(item => item.EvidenceBundleId).HasColumnName("evidence_bundle_id");
        builder.Property(item => item.Similarity).HasColumnName("similarity");
        builder.Property(item => item.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamp with time zone");
        builder.HasOne<IncidentClusterRecord>().WithMany().HasForeignKey(item => item.ClusterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<EvidenceBundleRecord>().WithOne().HasForeignKey<IncidentClusterOccurrenceRecord>(item => item.EvidenceBundleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => item.EvidenceBundleId).IsUnique().HasDatabaseName("ux_incident_cluster_occurrences_bundle");
        builder.HasIndex(item => new { item.ClusterId, item.OccurredAt }).HasDatabaseName("ix_incident_cluster_occurrences_cluster_occurred_at");
    }
}
