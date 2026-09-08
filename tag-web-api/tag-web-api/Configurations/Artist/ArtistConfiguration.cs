// <copyright file="ArtistConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations;

public class ArtistConfiguration : IEntityTypeConfiguration<Artist>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Artist> builder)
    {
        builder.HasKey(a => a.ArtistID);

        builder.Property(a => a.Applied)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(a => a.Byline)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(a => a.IncorporatedYear)
            .IsRequired(false);

        builder.Property(a => a.Country)
            .HasMaxLength(120)
            .IsRequired(false);

        builder.Property(a => a.StateOrProvince)
            .HasMaxLength(120)
            .IsRequired(false);

        builder.Property(a => a.BusinessEntityType)
            .HasMaxLength(80)
            .IsRequired(false);

        builder.Property(a => a.IsFormallyIncorporated)
            .IsRequired(false);

        builder.Property(a => a.IsPublished)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.IsModerationBlocked)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.Path)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(a => a.Path)
            .IsUnique();

        builder.Property(a => a.SEOTags)
            .HasMaxLength(255);

        builder.Property(a => a.Since)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(a => a.Statement)
            .HasMaxLength(1000);

        builder.Property(a => a.Title)
            .HasMaxLength(1000);

        builder.Property(a => a.CoverPicID).IsRequired(false);
        builder.HasOne(a => a.CoverPic)
            .WithMany()
            .HasForeignKey(a => a.CoverPicID)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(a => a.ProfilePicID).IsRequired(false);
        builder.HasOne(a => a.ProfilePic)
            .WithMany()
            .HasForeignKey(a => a.ProfilePicID)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(a => a.GalleryID)
            .IsRequired(false);

        builder.HasOne(a => a.Gallery)
            .WithMany()
            .HasForeignKey(a => a.GalleryID)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        SeedData(builder);
    }

    private static void SeedData(EntityTypeBuilder<Artist> builder)
    {
        var seedSince = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        builder.HasData(
            new Artist { ArtistID = 1, Title = "Twisted Passions", Byline = "Tie Dye Artisan", Statement = "The best damn tie dye, like EVER.", SEOTags = "tie dye, art, tshirts", Path = "TwistedPassions", ProfilePicID = 6, CoverPicID = 10, Since = seedSince },
            new Artist { ArtistID = 2, Title = "Art by Em", Byline = "Acrylic Paintings", Statement = "yes", SEOTags = "moon, acrylic", Path = "ArtByEm", ProfilePicID = null, CoverPicID = null, Since = seedSince },
            new Artist { ArtistID = 3, Title = "Queen City Cirque", Byline = "Fire flow performance", Statement = "Queen City Cirque is comprised of...", SEOTags = "fire flow, performance art", Path = "QC_Cirque", ProfilePicID = null, CoverPicID = null, Since = seedSince },
            new Artist { ArtistID = 4, Title = "Satarah", Byline = "To learn more about pricing and schedule your time with Satarah Productions please us contact today.", Statement = "Satarah is the lovechild of Satya and Sarah Hahn, two passionate and talented professional bellydancers, aerialists and fire performers that have come together to produce fantastic events and entertain the world. Between the two of them, they have been professionally performing for over 20 years, bringing a dynamic and exciting experience wherever they may go. Currently calling Charlotte home, this duo travels near and far to produce and perform at events and teach workshops. They have also recently opened studio Satarah, hosting all types of events in Charlotte.", SEOTags = "dance, bellydancer, aerialist, fire performer, duo", Path = "satarah", ProfilePicID = 1, CoverPicID = 16, Since = seedSince },
            new Artist { ArtistID = 5, Title = "DJ Kandy", Byline = "Amazing DJ services", Statement = "soooo good", SEOTags = "DJ, house", Path = "djKandy", ProfilePicID = null, CoverPicID = null, Since = seedSince },
            new Artist { ArtistID = 6, Title = "Saltwater Slide", Byline = "“Saltwater Slide is leading the way for the Texas reggae scene by not only promoting conscious messages through their music, but following through by their actions” — Topshelf Music", Statement = "Saltwater Slide is a San Antonio-based reggae/rock band that has quickly become a staple in both local and regional circles. Those who are familiar with their music are accustomed to their positive, relatable lyrics, high energy live performances, and active contribution to their local community. You can catch the guys at their annual Reggae Beach Cleanup in Corpus Christi, their band-managed Adopt-A-Spot on Mulberry road in San Antonio, or at venues all throughout Texas and beyond. Saltwater Slide has been inspired by acts like Fortunate Youth, Passafire, The Expanders, Arise Roots, Iya Terra, Tribal Seeds, Dubbest, Roots of a Rebellion, Pepper, The Skints, Rebelution, and more, although their unique style is hard to miss and harder to forget.", SEOTags = "reggea", Path = "saltwaterslide", ProfilePicID = null, CoverPicID = null, Since = seedSince },
            new Artist { ArtistID = 7, Title = "Campfire Cirque", Byline = "Fire Flow Performance Artist", Statement = "Long statement about how qualified i am!", SEOTags = "fire, poi", Path = "CampfireCirque", ProfilePicID = 17, CoverPicID = 18, Since = seedSince });
    }
}
