// <copyright file="GalleryConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations;

public class GalleryConfiguration : IEntityTypeConfiguration<Gallery>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Gallery> builder)
    {
        builder.HasKey(g => g.GalleryID);

        builder.Property(g => g.ScopeType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(g => g.ScopeEntityID)
            .IsRequired();

        builder.Property(g => g.Title)
            .HasMaxLength(250);

        builder.Property(g => g.Description)
            .HasColumnType("text");

        builder.Property(g => g.IsPrimary)
            .HasDefaultValue(false);

        builder.Property(g => g.Created)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(g => g.Updated)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(g => new { g.ScopeType, g.ScopeEntityID });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(g => g.OwnerUserID)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne<Artist>()
            .WithMany()
            .HasForeignKey(g => g.OwnerArtistID)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_Gallery_SingleOwner",
                "(CASE WHEN \"OwnerUserID\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"OwnerArtistID\" IS NOT NULL THEN 1 ELSE 0 END) <= 1");
        });
    }
}