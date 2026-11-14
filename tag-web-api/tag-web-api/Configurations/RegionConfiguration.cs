// <copyright file="RegionConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations;

public class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Region> builder)
    {
        builder.ToTable("Regions");

        builder.HasKey(r => r.RegionID);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(Region.NameMaxLength);

        builder.Property(r => r.Slug)
            .IsRequired()
            .HasMaxLength(Region.NameMaxLength);

        builder.Property(r => r.Type)
            .IsRequired()
            .HasMaxLength(20);

        // A list of state codes, stored as a text array.
        builder.Property(r => r.States)
            .IsRequired();

        builder.Property(r => r.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(r => r.Slug)
            .IsUnique();
    }
}

public class LinkerVenueToRegionConfiguration : IEntityTypeConfiguration<Linker_VenueToRegion>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Linker_VenueToRegion> builder)
    {
        builder.ToTable("Linker_VenueToRegion");

        builder.HasKey(l => l.Linker_VenueToRegionID);

        // A venue belongs to a given region at most once.
        builder.HasIndex(l => new { l.VenueID, l.RegionID })
            .IsUnique();

        builder.HasIndex(l => l.RegionID);

        // Links are removed together with their venue.
        builder.HasOne(l => l.Venue)
            .WithMany(v => v.RegionLinks)
            .HasForeignKey(l => l.VenueID)
            .OnDelete(DeleteBehavior.Cascade);

        // Regions are deactivated, never deleted, so a region that is still in use cannot be removed.
        builder.HasOne(l => l.Region)
            .WithMany()
            .HasForeignKey(l => l.RegionID)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class LinkerArtistToRegionConfiguration : IEntityTypeConfiguration<Linker_ArtistToRegion>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Linker_ArtistToRegion> builder)
    {
        builder.ToTable("Linker_ArtistToRegion");

        builder.HasKey(l => l.Linker_ArtistToRegionID);

        // An artist belongs to a given region at most once.
        builder.HasIndex(l => new { l.ArtistID, l.RegionID })
            .IsUnique();

        builder.HasIndex(l => l.RegionID);

        // Links are removed together with their artist.
        builder.HasOne<Artist>()
            .WithMany()
            .HasForeignKey(l => l.ArtistID)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Region)
            .WithMany()
            .HasForeignKey(l => l.RegionID)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
