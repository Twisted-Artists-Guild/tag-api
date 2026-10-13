// <copyright file="ListingConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations;

public class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Listing> builder)
    {
        builder.HasKey(l => l.ListingID);

        builder.Property(l => l.Title)
            .HasMaxLength(255);

        builder.Property(l => l.Description)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(l => l.CatalogueID)
            .HasMaxLength(255);

        builder.Property(l => l.Credits)
            .HasColumnType("text");

        builder.Property(l => l.Culture)
            .HasMaxLength(100);

        builder.Property(l => l.Date)
            .HasMaxLength(255);

        builder.Property(l => l.Department)
            .HasMaxLength(100);

        builder.Property(l => l.Locale)
            .HasMaxLength(100);

        builder.Property(l => l.Locus)
            .HasMaxLength(100);

        builder.Property(l => l.Medium)
            .HasColumnType("text");

        builder.Property(l => l.Period)
            .HasMaxLength(100);

        builder.Property(l => l.Repository)
            .HasMaxLength(100);

        builder.Property(l => l.Rights)
            .HasMaxLength(255);

        builder.Property(l => l.TaxJurisdiction)
            .HasMaxLength(100);

        builder.Property(l => l.Path)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(l => l.ArtCategoryID)
            .IsRequired();

        builder.Property(l => l.ArtistID)
            .IsRequired();

        builder.Property(l => l.Created)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(l => l.CoverPicID)
            .IsRequired(false);

        builder.Property(l => l.GalleryID)
            .IsRequired(false);

        builder.Property(l => l.IsPublished)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(l => l.IsModerationBlocked)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasOne(l => l.CoverPic)
            .WithMany()
            .HasForeignKey(l => l.CoverPicID)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(l => l.Gallery)
            .WithMany()
            .HasForeignKey(l => l.GalleryID)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(l => l.ArtCategory)
            .WithMany()
            .HasForeignKey(l => l.ArtCategoryID)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(l => l.Artist)
            .WithMany(a => a.Listings)
            .HasForeignKey(l => l.ArtistID)
            .OnDelete(DeleteBehavior.SetNull);

        // Create a compound index for unique artist-listing paths
        builder.HasIndex("ArtistID", "Path")
            .IsUnique()
            .HasDatabaseName("IX_Listing_ArtistID_Path");

        SeedData(builder);
    }

    private static void SeedData(EntityTypeBuilder<Listing> builder)
    {
        builder.HasData(
            new Listing
            {
                ListingID = 1,
                Title = "Studio Satarah Booty Shorts",
                Description = "Funny when they kiss!",
                ArtCategoryID = 30, // merchandise
                Price = 199.99m,
                Path = "booty-shorts",
                ArtistID = 4,
                // ...other properties...
            },
            new Listing
            {
                ListingID = 2,
                Title = "Tie Dye Shirt",
                Description = "A gorgeous custom tie dye",
                ArtCategoryID = 3, // Assuming 'Tie Dye' has ArtCategoryID = 3
                Price = 19.99m,
                Path = "tiedye1",
                ArtistID = 1,
                // ...other properties...
            },
            new Listing
            {
                ListingID = 3,
                Title = "Tie Dye Shirt2",
                Description = "A gorgeous custom tie dye",
                ArtCategoryID = 3, // Assuming 'Tie Dye' has ArtCategoryID = 3
                Price = 29.99m,
                Path = "tiedye2",
                ArtistID = 1,
                // ...other properties...
            },
            new Listing
            {
                ListingID = 4,
                Title = "Tie Dye Shirt3",
                Description = "A gorgeous custom tie dye",
                ArtCategoryID = 3, // Assuming 'Tie Dye' has ArtCategoryID = 3
                Price = 39.99m,
                Path = "tiedye3",
                ArtistID = 1,
                // ...other properties...
            }
        );
    }
}
