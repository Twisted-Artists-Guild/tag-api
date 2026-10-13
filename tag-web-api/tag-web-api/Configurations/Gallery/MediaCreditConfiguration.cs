// <copyright file="MediaCreditConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations;

public class MediaCreditConfiguration : IEntityTypeConfiguration<MediaCredit>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<MediaCredit> builder)
    {
        builder.HasKey(mc => mc.MediaCreditID);

        builder.Property(mc => mc.CreditText)
            .HasMaxLength(2000);

        builder.Property(mc => mc.SortOrder)
            .HasDefaultValue(0);

        builder.HasOne<Picture>()
            .WithMany()
            .HasForeignKey(mc => mc.PictureID)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        builder.HasOne<Video>()
            .WithMany()
            .HasForeignKey(mc => mc.VideoID)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        builder.HasOne<Blog>()
            .WithMany()
            .HasForeignKey(mc => mc.BlogID)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        builder.HasOne<CreditParty>()
            .WithMany()
            .HasForeignKey(mc => mc.CreditPartyID)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne<CreditRole>()
            .WithMany()
            .HasForeignKey(mc => mc.CreditRoleID)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasIndex(mc => new { mc.PictureID, mc.CreditPartyID, mc.CreditRoleID })
            .HasFilter("\"PictureID\" IS NOT NULL")
            .IsUnique();

        builder.HasIndex(mc => new { mc.VideoID, mc.CreditPartyID, mc.CreditRoleID })
            .HasFilter("\"VideoID\" IS NOT NULL")
            .IsUnique();

        builder.HasIndex(mc => new { mc.BlogID, mc.CreditPartyID, mc.CreditRoleID })
            .HasFilter("\"BlogID\" IS NOT NULL")
            .IsUnique();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_MediaCredit_ExactlyOneMedia",
                "(CASE WHEN \"PictureID\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"VideoID\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"BlogID\" IS NOT NULL THEN 1 ELSE 0 END) = 1");
        });

        SeedData(builder);
    }

    private static void SeedData(EntityTypeBuilder<MediaCredit> builder)
    {
        builder.HasData(
            new MediaCredit { MediaCreditID = 1, PictureID = 1, CreditPartyID = 1, CreditRoleID = 1, CreditText = "Copyright owner", SortOrder = 1 },
            new MediaCredit { MediaCreditID = 2, PictureID = 1, CreditPartyID = 2, CreditRoleID = 2, CreditText = "Principal photographer", SortOrder = 2 });
    }
}