// <copyright file="LinkerArtistToCategoryConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations;

public class LinkerArtistToCategoryConfiguration : IEntityTypeConfiguration<LinkerArtistToCategory>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LinkerArtistToCategory> builder)
    {
        builder.HasKey(lac => lac.Linker_ArtistToCategoryID);

        builder.Property(lac => lac.ArtistID)
            .IsRequired();

        builder.Property(lac => lac.ArtistCategoryID)
            .IsRequired();

        builder.Property(lac => lac.Genre)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(lac => lac.ExpertiseLevel)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(lac => lac.IsProfessional)
            .IsRequired();

        builder.HasOne(lac => lac.Artist)
            .WithMany(a => a.ArtistCategoryLinks)
            .HasForeignKey(lac => lac.ArtistID)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(lac => lac.ArtistCategory)
            .WithMany(ac => ac.ArtistLinks)
            .HasForeignKey(lac => lac.ArtistCategoryID)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(lac => new { lac.ArtistID, lac.ArtistCategoryID })
            .IsUnique();

        SeedData(builder);
    }

    private static void SeedData(EntityTypeBuilder<LinkerArtistToCategory> builder)
    {
        builder.HasData(
            new LinkerArtistToCategory { Linker_ArtistToCategoryID = 1, ArtistID = 1, ArtistCategoryID = 29, Genre = "Tie Dye", ExpertiseLevel = "Advanced", IsProfessional = true },
            new LinkerArtistToCategory { Linker_ArtistToCategoryID = 2, ArtistID = 2, ArtistCategoryID = 16, Genre = "Acrylic Painting", ExpertiseLevel = "Intermediate", IsProfessional = false },
            new LinkerArtistToCategory { Linker_ArtistToCategoryID = 3, ArtistID = 3, ArtistCategoryID = 45, Genre = "Fire Flow", ExpertiseLevel = "Advanced", IsProfessional = true },
            new LinkerArtistToCategory { Linker_ArtistToCategoryID = 4, ArtistID = 4, ArtistCategoryID = 43, Genre = "Aerial Arts", ExpertiseLevel = "Advanced", IsProfessional = true },
            new LinkerArtistToCategory { Linker_ArtistToCategoryID = 5, ArtistID = 5, ArtistCategoryID = 10, Genre = "House Music", ExpertiseLevel = "Advanced", IsProfessional = true },
            new LinkerArtistToCategory { Linker_ArtistToCategoryID = 6, ArtistID = 6, ArtistCategoryID = 40, Genre = "Reggae Rock", ExpertiseLevel = "Advanced", IsProfessional = true },
            new LinkerArtistToCategory { Linker_ArtistToCategoryID = 7, ArtistID = 7, ArtistCategoryID = 45, Genre = "Fire Flow", ExpertiseLevel = "Intermediate", IsProfessional = true });
    }
}