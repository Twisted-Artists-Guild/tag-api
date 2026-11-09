// <copyright file="LinkerUserToVenueConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations;

public class LinkerUserToVenueConfiguration : IEntityTypeConfiguration<Linker_UserToVenue>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Linker_UserToVenue> builder)
    {
        builder.ToTable("Linker_UserToVenue");

        builder.HasKey(l => l.Linker_UserToVenueID);

        builder.Property(l => l.UserID)
            .IsRequired();

        builder.Property(l => l.VenueID)
            .IsRequired();

        builder.Property(l => l.Role)
            .IsRequired()
            .HasMaxLength(255);

        // A user is linked to a given venue at most once.
        builder.HasIndex(l => new { l.UserID, l.VenueID })
            .IsUnique();

        builder.HasIndex(l => l.VenueID);

        // Links are removed together with their venue.
        builder.HasOne(l => l.Venue)
            .WithMany()
            .HasForeignKey(l => l.VenueID)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
