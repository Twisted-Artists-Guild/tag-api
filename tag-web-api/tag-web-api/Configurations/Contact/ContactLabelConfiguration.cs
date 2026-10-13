// <copyright file="ContactLabelConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models.Configurations.Contact
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using TAGWEBAPI.Models;

    public class ContactLabelConfiguration : IEntityTypeConfiguration<ContactLabel>
    {
        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<ContactLabel> builder)
        {
            builder.HasKey(cl => cl.ContactLabelID);

            builder.Property(cl => cl.Label)
                .IsRequired()
                .HasMaxLength(100);

            builder.HasIndex(cl => cl.Label)
                .IsUnique();

            SeedData(builder);
        }

        private static void SeedData(EntityTypeBuilder<ContactLabel> builder)
        {
            builder.HasData(
                new ContactLabel { ContactLabelID = 1, Label = "home" },
                new ContactLabel { ContactLabelID = 2, Label = "work" },
                new ContactLabel { ContactLabelID = 3, Label = "mobile" },
                new ContactLabel { ContactLabelID = 4, Label = "office" },
                new ContactLabel { ContactLabelID = 5, Label = "studio" },
                new ContactLabel { ContactLabelID = 6, Label = "regional office" },
                new ContactLabel { ContactLabelID = 7, Label = "support" },
                new ContactLabel { ContactLabelID = 8, Label = "booking" },
                new ContactLabel { ContactLabelID = 9, Label = "press" },
                new ContactLabel { ContactLabelID = 10, Label = "billing" },
                new ContactLabel { ContactLabelID = 11, Label = "sales" },
                new ContactLabel { ContactLabelID = 12, Label = "hq" },
                new ContactLabel { ContactLabelID = 13, Label = "warehouse" },
                new ContactLabel { ContactLabelID = 14, Label = "branch" },
                new ContactLabel { ContactLabelID = 15, Label = "other" });
        }
    }
}
