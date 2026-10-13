// <copyright file="CreditRoleConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations;

public class CreditRoleConfiguration : IEntityTypeConfiguration<CreditRole>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<CreditRole> builder)
    {
        builder.HasKey(cr => cr.CreditRoleID);

        builder.Property(cr => cr.KeyName)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(cr => cr.Label)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(cr => cr.DisplayOrder)
            .HasDefaultValue(0);

        builder.Property(cr => cr.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(cr => cr.KeyName)
            .IsUnique();

        SeedData(builder);
    }

    private static void SeedData(EntityTypeBuilder<CreditRole> builder)
    {
        builder.HasData(
            new CreditRole { CreditRoleID = 1, KeyName = "copyright-owner", Label = "Copyright Owner", DisplayOrder = 1, IsActive = true },
            new CreditRole { CreditRoleID = 2, KeyName = "photographer", Label = "Photographer", DisplayOrder = 2, IsActive = true },
            new CreditRole { CreditRoleID = 3, KeyName = "videographer", Label = "Videographer", DisplayOrder = 3, IsActive = true },
            new CreditRole { CreditRoleID = 4, KeyName = "model", Label = "Model", DisplayOrder = 4, IsActive = true },
            new CreditRole { CreditRoleID = 5, KeyName = "makeup-artist", Label = "Makeup Artist", DisplayOrder = 5, IsActive = true },
            new CreditRole { CreditRoleID = 6, KeyName = "set-designer", Label = "Set Designer", DisplayOrder = 6, IsActive = true },
            new CreditRole { CreditRoleID = 7, KeyName = "designer", Label = "Designer", DisplayOrder = 7, IsActive = true },
            new CreditRole { CreditRoleID = 8, KeyName = "tech-crew", Label = "Tech Crew", DisplayOrder = 8, IsActive = true },
            new CreditRole { CreditRoleID = 9, KeyName = "lighting-crew", Label = "Lighting Crew", DisplayOrder = 9, IsActive = true },
            new CreditRole { CreditRoleID = 10, KeyName = "stage-crew", Label = "Stage Crew", DisplayOrder = 10, IsActive = true },
            new CreditRole { CreditRoleID = 11, KeyName = "audio-crew", Label = "Audio Crew", DisplayOrder = 11, IsActive = true },
            new CreditRole { CreditRoleID = 12, KeyName = "editor", Label = "Editor", DisplayOrder = 12, IsActive = true },
            new CreditRole { CreditRoleID = 13, KeyName = "producer", Label = "Producer", DisplayOrder = 13, IsActive = true },
            new CreditRole { CreditRoleID = 14, KeyName = "assistant", Label = "Assistant", DisplayOrder = 14, IsActive = true },
            new CreditRole { CreditRoleID = 15, KeyName = "stylist", Label = "Stylist", DisplayOrder = 15, IsActive = true },
            new CreditRole { CreditRoleID = 16, KeyName = "wardrobe", Label = "Wardrobe", DisplayOrder = 16, IsActive = true });
    }
}