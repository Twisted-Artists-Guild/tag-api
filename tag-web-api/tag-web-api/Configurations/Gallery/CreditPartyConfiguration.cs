// <copyright file="CreditPartyConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations;

public class CreditPartyConfiguration : IEntityTypeConfiguration<CreditParty>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<CreditParty> builder)
    {
        builder.HasKey(cp => cp.CreditPartyID);

        builder.Property(cp => cp.DisplayName)
            .HasMaxLength(250);

        builder.Property(cp => cp.ExternalURL)
            .HasColumnType("text");

        builder.Property(cp => cp.BioNote)
            .HasMaxLength(2000);

        builder.Property(cp => cp.Created)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(cp => cp.UserID)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne<Artist>()
            .WithMany()
            .HasForeignKey(cp => cp.ArtistID)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_CreditParty_HasIdentity",
                "(CASE WHEN \"UserID\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"ArtistID\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"DisplayName\" IS NOT NULL AND LENGTH(TRIM(\"DisplayName\")) > 0 THEN 1 ELSE 0 END) >= 1");
        });

        SeedData(builder);
    }

    private static void SeedData(EntityTypeBuilder<CreditParty> builder)
    {
        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(
            new CreditParty { CreditPartyID = 1, DisplayName = "Twisted Artists Guild", ExternalURL = "https://twistedartistsguild.com", BioNote = "Collective rights holder and publication organizer.", Created = seedDate },
            new CreditParty { CreditPartyID = 2, DisplayName = "Satarah Visuals", ExternalURL = "https://instagram.com/satarah", BioNote = "Photography and direction.", Created = seedDate },
            new CreditParty { CreditPartyID = 3, DisplayName = "Lumen Stagecraft", ExternalURL = "https://example.com/lumen-stagecraft", BioNote = "Lighting and stage systems crew.", Created = seedDate },
            new CreditParty { CreditPartyID = 4, DisplayName = "Backline Builders", ExternalURL = "https://example.com/backline-builders", BioNote = "Stage support and technical coordination.", Created = seedDate });
    }
}