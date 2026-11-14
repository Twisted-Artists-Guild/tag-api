// <copyright file="VenueTagConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations;

public class VenueTagConfiguration : IEntityTypeConfiguration<VenueTag>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<VenueTag> builder)
    {
        builder.ToTable("VenueTags");

        builder.HasKey(t => t.VenueTagID);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(VenueTag.NameMaxLength);

        // Names are stored normalized, so this keeps one row per tag.
        builder.HasIndex(t => t.Name)
            .IsUnique();
    }
}

public class LinkerVenueToTagConfiguration : IEntityTypeConfiguration<Linker_VenueToTag>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Linker_VenueToTag> builder)
    {
        builder.ToTable("Linker_VenueToTag");

        builder.HasKey(l => l.Linker_VenueToTagID);

        // A venue has a given tag at most once.
        builder.HasIndex(l => new { l.VenueID, l.VenueTagID })
            .IsUnique();

        builder.HasIndex(l => l.VenueTagID);

        builder.HasOne(l => l.Venue)
            .WithMany(v => v.TagLinks)
            .HasForeignKey(l => l.VenueID)
            .OnDelete(DeleteBehavior.Cascade);

        // Staff can remove a tag, which takes it off every venue.
        builder.HasOne(l => l.Tag)
            .WithMany()
            .HasForeignKey(l => l.VenueTagID)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
