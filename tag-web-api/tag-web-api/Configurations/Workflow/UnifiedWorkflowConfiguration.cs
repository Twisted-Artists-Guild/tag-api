// <copyright file="UnifiedWorkflowConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations;

/// <summary>
/// Configuration for the UnifiedWorkflow entity.
/// </summary>
public class UnifiedWorkflowConfiguration : IEntityTypeConfiguration<UnifiedWorkflow>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<UnifiedWorkflow> builder)
    {
        builder.HasKey(w => w.UnifiedWorkflowID);

        builder.Property(w => w.EntityID)
            .IsRequired();

        builder.Property(w => w.EntityType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(w => w.WorkflowName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(w => w.StepKey)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(w => w.IsCompleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(w => w.CompletedAt)
            .IsRequired(false);

        builder.Property(w => w.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(w => w.UpdatedByUserID)
            .IsRequired(false);

        // Unique constraint: one workflow step per entity/workflow/step key
        builder.HasIndex(w => new { w.EntityID, w.EntityType, w.WorkflowName, w.StepKey })
            .IsUnique();

        // Composite index for efficient workflow lookups
        builder.HasIndex(w => new { w.EntityID, w.EntityType, w.WorkflowName });

        // Index for completion status queries
        builder.HasIndex(w => new { w.EntityID, w.EntityType, w.IsCompleted });

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_UnifiedWorkflow_ValidEntityType",
                "\"EntityType\" IN ('user', 'artist', 'venue', 'vendor', 'listing')");
        });
    }
}
