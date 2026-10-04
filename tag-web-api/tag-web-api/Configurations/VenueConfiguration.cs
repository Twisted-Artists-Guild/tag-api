// <copyright file="VenueConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models.Configurations
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using TAGWEBAPI.Models;

    public class VenueConfiguration : IEntityTypeConfiguration<Venue>
    {
        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<Venue> builder)
        {
            builder.HasKey(v => v.VenueID);

            builder.Property(v => v.Name)
                .HasMaxLength(1000);

            builder.Property(v => v.AddressID)
                .IsRequired();

            builder.Property(v => v.ExternalLinkID)
                .IsRequired();

            builder.Property(v => v.PhoneContactID)
                .IsRequired();

            builder.Property(v => v.LogoPicID)
                .IsRequired(false);

            builder.HasOne(v => v.LogoPic)
                .WithMany()
                .HasForeignKey(v => v.LogoPicID)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Property(v => v.IsPublished)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(v => v.IsModerationBlocked)
                .IsRequired()
                .HasDefaultValue(false);

            SeedData(builder);
        }

        private static void SeedData(EntityTypeBuilder<Venue> builder)
        {
            builder.HasData(
                new Venue { VenueID = 1, Name = "Coffee House", AddressID = 1, ExternalLinkID = 1, PhoneContactID = 1 },
                new Venue { VenueID = 2, Name = "Large Venue", AddressID = 2, ExternalLinkID = 2, PhoneContactID = 2 },
                new Venue { VenueID = 3, Name = "Art Studio", AddressID = 3, ExternalLinkID = 3, PhoneContactID = 3 },
                new Venue { VenueID = 4, Name = "Classroom", AddressID = 4, ExternalLinkID = 4, PhoneContactID = 4 },
                new Venue { VenueID = 5, Name = "Satarah Studio", AddressID = 10, ExternalLinkID = 5, PhoneContactID = 5 }
            );
        }
    }
}
