// <copyright file="ContactConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models.Configurations.Contact
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using TAGWEBAPI.Models;

    public class ContactConfiguration : IEntityTypeConfiguration<Contact>
    {
        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<Contact> builder)
        {
            builder.HasKey(c => c.ContactID);

            builder.Property(c => c.ContactType)
                .IsRequired()
                .HasMaxLength(32);

            builder.Property(c => c.Label)
                .HasMaxLength(100);

            builder.Property(c => c.ContactLabelID)
                .IsRequired(false);

            builder.Property(c => c.Category)
                .HasMaxLength(100);

            builder.Property(c => c.Value)
                .HasMaxLength(1000);

            builder.Property(c => c.Handle)
                .HasMaxLength(255);

            builder.Property(c => c.Description)
                .HasMaxLength(255);

            builder.HasOne(c => c.Address)
                .WithOne()
                .HasForeignKey<Contact>(c => c.AddressID)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasOne(c => c.PhoneContact)
                .WithOne()
                .HasForeignKey<Contact>(c => c.PhoneContactID)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasOne(c => c.ContactLabel)
                .WithMany()
                .HasForeignKey(c => c.ContactLabelID)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasIndex(c => c.AddressID)
                .IsUnique()
                .HasFilter("\"AddressID\" IS NOT NULL");

            builder.HasIndex(c => c.PhoneContactID)
                .IsUnique()
                .HasFilter("\"PhoneContactID\" IS NOT NULL");

            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Contacts_ContactType", "\"ContactType\" IN ('address', 'phone', 'email', 'url')");
                t.HasCheckConstraint(
                    "CK_Contacts_TypePayload",
                    "(\"ContactType\" = 'address' AND \"AddressID\" IS NOT NULL AND \"PhoneContactID\" IS NULL AND \"Value\" IS NULL) OR " +
                    "(\"ContactType\" = 'phone' AND \"PhoneContactID\" IS NOT NULL AND \"AddressID\" IS NULL AND \"Value\" IS NULL) OR " +
                    "(\"ContactType\" = 'email' AND \"Value\" IS NOT NULL AND \"AddressID\" IS NULL AND \"PhoneContactID\" IS NULL) OR " +
                    "(\"ContactType\" = 'url' AND \"Value\" IS NOT NULL AND \"AddressID\" IS NULL AND \"PhoneContactID\" IS NULL)");
            });

            SeedData(builder);
        }

        private static void SeedData(EntityTypeBuilder<Contact> builder)
        {
            builder.HasData(
                new Contact
                {
                    ContactID = 1001,
                    ContactType = "email",
                    Label = "Business Email",
                    Category = "studio",
                    Value = "hello@satarahpresents.com",
                    Description = "General inquiries",
                },
                new Contact
                {
                    ContactID = 1002,
                    ContactType = "url",
                    Label = "Official Website",
                    Category = "portfolio",
                    Value = "https://satarahpresents.com",
                    Description = "Main website",
                    Handle = "satarahpresents",
                },
                new Contact
                {
                    ContactID = 1003,
                    ContactType = "phone",
                    Label = "Studio Phone",
                    Category = "work",
                    PhoneContactID = 4,
                    Description = "Calls and text",
                },
                new Contact
                {
                    ContactID = 1004,
                    ContactType = "address",
                    Label = "Studio Location",
                    Category = "studio",
                    AddressID = 10,
                });
        }
    }
}