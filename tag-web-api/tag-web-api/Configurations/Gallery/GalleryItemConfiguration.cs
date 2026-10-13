// <copyright file="GalleryItemConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations;

public class GalleryItemConfiguration : IEntityTypeConfiguration<GalleryItem>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<GalleryItem> builder)
    {
        builder.HasKey(gi => gi.GalleryItemID);

        builder.Property(gi => gi.SortOrder)
            .HasDefaultValue(0);

        builder.Property(gi => gi.CaptionOverride)
            .HasColumnType("text");

        builder.Property(gi => gi.Created)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne<Gallery>()
            .WithMany(g => g.GalleryItems)
            .HasForeignKey(gi => gi.GalleryID)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne(gi => gi.Picture)
            .WithMany()
            .HasForeignKey(gi => gi.PictureID)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        builder.HasOne(gi => gi.Video)
            .WithMany()
            .HasForeignKey(gi => gi.VideoID)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(gi => gi.AddedByUserID)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasIndex(gi => new { gi.GalleryID, gi.SortOrder })
            .IsUnique();

        builder.HasIndex(gi => new { gi.GalleryID, gi.PictureID })
            .HasFilter("\"PictureID\" IS NOT NULL")
            .IsUnique();

        builder.HasIndex(gi => new { gi.GalleryID, gi.VideoID })
            .HasFilter("\"VideoID\" IS NOT NULL")
            .IsUnique();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_GalleryItem_ExactlyOneMedia",
                "(CASE WHEN \"PictureID\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"VideoID\" IS NOT NULL THEN 1 ELSE 0 END) = 1");
        });
    }
}