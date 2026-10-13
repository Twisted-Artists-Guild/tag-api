// <copyright file="AddressConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations.Contact;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.HasKey(a => a.AddressID);

        builder.Property(a => a.AddressLine1)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.AddressLine2)
            .HasMaxLength(255);

        builder.Property(a => a.AddressLine3)
            .HasMaxLength(255);

        builder.Property(a => a.AddressLine4)
            .HasMaxLength(255);

        builder.Property(a => a.City)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Country)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.Region).IsRequired(false)
            .HasMaxLength(100);

        builder.Property(a => a.State)
            .HasMaxLength(20);

        builder.Property(a => a.ZipCode)
            .HasMaxLength(50);

        builder.Property(a => a.OperationHours)
            .HasMaxLength(255);

        builder.Property(a => a.ContactLabelID)
            .IsRequired(false);

        builder.HasOne(a => a.ContactLabel)
            .WithMany()
            .HasForeignKey(a => a.ContactLabelID)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        SeedData(builder);
    }

    private static void SeedData(EntityTypeBuilder<Address> builder)
    {
        builder.HasData(
            new Address { AddressID = 1, AddressLine1 = "123 Peach St", City = "Atlanta", State = "GA", Country = "USA", ZipCode = "30301", OperationHours = "Mon-Fri 9am-5pm" },
            new Address { AddressID = 2, AddressLine1 = "456 Oak Dr", City = "Savannah", State = "GA", Country = "USA", ZipCode = "31401", OperationHours = "Mon-Sat 10am-6pm" },
            new Address { AddressID = 3, AddressLine1 = "789 Pine Ln", City = "Augusta", State = "GA", Country = "USA", ZipCode = "30901", OperationHours = "Mon-Fri 9am-5pm" },
            new Address { AddressID = 4, AddressLine1 = "123 Trade St", City = "Charlotte", State = "NC", Country = "USA", ZipCode = "28202", OperationHours = "Mon-Fri: 10am-6pm, Sat: 11am-5pm, Sun: Closed" },
            new Address { AddressID = 5, AddressLine1 = "202 Cedar St", City = "Raleigh", State = "NC", Country = "USA", ZipCode = "27601", OperationHours = "Mon-Fri 9am-5pm" },
            new Address { AddressID = 6, AddressLine1 = "303 Birch Blvd", City = "Greensboro", State = "NC", Country = "USA", ZipCode = "27401", OperationHours = "Mon-Fri 9am-5pm" },
            new Address { AddressID = 7, AddressLine1 = "404 Elm St", City = "Charleston", State = "SC", Country = "USA", ZipCode = "29401", OperationHours = "Mon-Fri 9am-5pm" },
            new Address { AddressID = 8, AddressLine1 = "505 Spruce Dr", City = "Columbia", State = "SC", Country = "USA", ZipCode = "29201", OperationHours = "Mon-Fri 9am-5pm" },
            new Address { AddressID = 9, AddressLine1 = "606 Willow Way", City = "Greenville", State = "SC", Country = "USA", ZipCode = "29601", OperationHours = "Mon-Fri 8am-4pm" },
            new Address { AddressID = 10, AddressLine1 = "583 Aerial Heights Lane", City = "Charlotte", State = "North Carolina", Country = "USA", ZipCode = "28202", OperationHours = "Mon-Fri 9am-5pm" }
        );
    }
}
