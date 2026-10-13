// <copyright file="ListingSalesItemConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Models.Configurations;

public class ListingSalesItemConfiguration : IEntityTypeConfiguration<ListingSalesItem>
{
    public void Configure(EntityTypeBuilder<ListingSalesItem> builder)
    {
        builder.HasKey(l => l.ListingSalesItemNum);

        builder.Property(l => l.InventoryRemaining);
        builder.Property(l => l.QuantitySold);

        builder.HasOne(l => l.DigitalDeliverySpecs)
            .WithMany()
            .HasForeignKey(l => l.DigitalDeliverySpecsID)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(l => l.ShippingSpecs)
            .WithMany()
            .HasForeignKey(l => l.ShippingSpecsID)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(l => l.TicketType)
            .WithMany()
            .HasForeignKey(l => l.TicketTypeID)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(l => l.Listing)
            .WithMany(l => l.ListingSalesItems)
            .HasForeignKey(l => l.ListingID)
            .OnDelete(DeleteBehavior.Cascade);
    }
}