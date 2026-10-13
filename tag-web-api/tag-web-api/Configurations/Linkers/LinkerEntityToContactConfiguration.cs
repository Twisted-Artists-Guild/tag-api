// <copyright file="LinkerEntityToContactConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations;

public class LinkerEntityToContactConfiguration : IEntityTypeConfiguration<Linker_EntityToContact>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Linker_EntityToContact> builder)
    {
        builder.HasKey(l => l.Linker_EntityToContactID);

        builder.Property(l => l.EntityType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(l => l.EntityID)
            .IsRequired();

        builder.Property(l => l.Scope)
            .HasDefaultValue(ContactScope.Secondary);

        builder.Property(l => l.DisplayOrder)
            .HasDefaultValue(0);

        builder.HasOne(l => l.Contact)
            .WithMany(c => c.EntityLinks)
            .HasForeignKey(l => l.ContactID)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasIndex(l => new { l.EntityType, l.EntityID, l.ContactID })
            .IsUnique();

        builder.HasIndex(l => new { l.EntityType, l.EntityID });

        SeedData(builder);
    }

    private static void SeedData(EntityTypeBuilder<Linker_EntityToContact> builder)
    {
        builder.HasData(
            new Linker_EntityToContact
            {
                Linker_EntityToContactID = 1001,
                EntityType = LinkedEntityTypes.Artist,
                EntityID = 4,
                ContactID = 1001,
                Scope = ContactScope.Primary,
                DisplayOrder = 1,
            },
            new Linker_EntityToContact
            {
                Linker_EntityToContactID = 1002,
                EntityType = LinkedEntityTypes.Artist,
                EntityID = 4,
                ContactID = 1002,
                Scope = ContactScope.Secondary,
                DisplayOrder = 2,
            },
            new Linker_EntityToContact
            {
                Linker_EntityToContactID = 1003,
                EntityType = LinkedEntityTypes.Artist,
                EntityID = 4,
                ContactID = 1003,
                Scope = ContactScope.Secondary,
                DisplayOrder = 3,
            },
            new Linker_EntityToContact
            {
                Linker_EntityToContactID = 1004,
                EntityType = LinkedEntityTypes.Artist,
                EntityID = 4,
                ContactID = 1004,
                Scope = ContactScope.Secondary,
                DisplayOrder = 4,
            });
    }
}