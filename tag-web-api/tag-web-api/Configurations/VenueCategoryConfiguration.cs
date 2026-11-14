// <copyright file="VenueCategoryConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations;

public class VenueCategoryConfiguration : IEntityTypeConfiguration<VenueCategory>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<VenueCategory> builder)
    {
        builder.ToTable("VenueCategories");

        builder.HasKey(c => c.VenueCategoryID);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(VenueCategory.NameMaxLength);

        builder.Property(c => c.Slug)
            .IsRequired()
            .HasMaxLength(VenueCategory.NameMaxLength);

        builder.Property(c => c.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(c => c.Slug)
            .IsUnique();
    }
}

public class LinkerVenueToCategoryConfiguration : IEntityTypeConfiguration<Linker_VenueToCategory>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Linker_VenueToCategory> builder)
    {
        builder.ToTable("Linker_VenueToCategory");

        builder.HasKey(l => l.Linker_VenueToCategoryID);

        // A venue has a given category at most once.
        builder.HasIndex(l => new { l.VenueID, l.VenueCategoryID })
            .IsUnique();

        builder.HasIndex(l => l.VenueCategoryID);

        // Links are removed together with their venue.
        builder.HasOne(l => l.Venue)
            .WithMany(v => v.CategoryLinks)
            .HasForeignKey(l => l.VenueID)
            .OnDelete(DeleteBehavior.Cascade);

        // Categories are deactivated, never deleted, so a category that is still in use cannot be removed.
        builder.HasOne(l => l.Category)
            .WithMany()
            .HasForeignKey(l => l.VenueCategoryID)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
