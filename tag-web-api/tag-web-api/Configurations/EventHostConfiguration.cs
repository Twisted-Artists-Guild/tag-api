// <copyright file="EventHostConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations;

public class EventHostConfiguration : IEntityTypeConfiguration<EventHost>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<EventHost> builder)
    {
        builder.ToTable("EventHosts");

        builder.HasKey(h => h.EventHostID);

        builder.Property(h => h.HostType)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(h => h.Role)
            .HasConversion<int>();

        // A user, artist or venue takes part in an event once, with one role.
        builder.HasIndex(h => new { h.EventID, h.HostType, h.HostID })
            .IsUnique();

        builder.HasIndex(h => new { h.HostType, h.HostID });

        builder.HasOne(h => h.Event)
            .WithMany()
            .HasForeignKey(h => h.EventID)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
