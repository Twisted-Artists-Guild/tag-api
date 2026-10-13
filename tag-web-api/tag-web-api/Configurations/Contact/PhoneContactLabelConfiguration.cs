// <copyright file="PhoneContactLabelConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models.Configurations.Contact
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using TAGWEBAPI.Models;

    public class PhoneContactLabelConfiguration : IEntityTypeConfiguration<PhoneContactLabel>
    {
        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<PhoneContactLabel> builder)
        {
            builder.HasKey(pcl => pcl.PhoneContactLabelID);

            builder.Property(pcl => pcl.Label)
                .IsRequired()
                .HasMaxLength(50);

            builder.HasIndex(pcl => pcl.Label)
                .IsUnique();

            SeedData(builder);
        }

        private static void SeedData(EntityTypeBuilder<PhoneContactLabel> builder)
        {
            builder.HasData(
                new PhoneContactLabel { PhoneContactLabelID = 1, Label = "home" },
                new PhoneContactLabel { PhoneContactLabelID = 2, Label = "office" },
                new PhoneContactLabel { PhoneContactLabelID = 3, Label = "mobile" },
                new PhoneContactLabel { PhoneContactLabelID = 4, Label = "other" });
        }
    }
}
