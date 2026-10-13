// <copyright file="LinkerUserToArtistConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations;

public class LinkerUserToArtistConfiguration : IEntityTypeConfiguration<Linker_UserToArtist>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Linker_UserToArtist> builder)
    {
        builder.HasKey(uta => uta.Linker_UserToArtistID);

        builder.Property(uta => uta.UserID)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(uta => uta.UserID)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(uta => uta.ArtistID)
            .IsRequired();

        builder.HasOne<Artist>()
            .WithMany()
            .HasForeignKey(uta => uta.ArtistID)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(uta => uta.Role)
            .IsRequired()
            .HasMaxLength(255);

        SeedData(builder);
    }

    private static void SeedData(EntityTypeBuilder<Linker_UserToArtist> builder)
    {
        builder.HasData(
            new Linker_UserToArtist { Linker_UserToArtistID = 1, UserID = 1, ArtistID = 1, Role = "Member" },
            new Linker_UserToArtist { Linker_UserToArtistID = 2, UserID = 2, ArtistID = 2, Role = "Member" },
            new Linker_UserToArtist { Linker_UserToArtistID = 3, UserID = 3, ArtistID = 3, Role = "Member" },
            new Linker_UserToArtist { Linker_UserToArtistID = 4, UserID = 5, ArtistID = 3, Role = "Member" },
            new Linker_UserToArtist { Linker_UserToArtistID = 5, UserID = 6, ArtistID = 3, Role = "Member" },
            new Linker_UserToArtist { Linker_UserToArtistID = 6, UserID = 3, ArtistID = 4, Role = "Member" },
            new Linker_UserToArtist { Linker_UserToArtistID = 7, UserID = 6, ArtistID = 4, Role = "Member" },
            new Linker_UserToArtist { Linker_UserToArtistID = 8, UserID = 2, ArtistID = 5, Role = "Member" },
            new Linker_UserToArtist { Linker_UserToArtistID = 9, UserID = 4, ArtistID = 6, Role = "Member" },
            new Linker_UserToArtist { Linker_UserToArtistID = 10, UserID = 1, ArtistID = 7, Role = "Member" });
    }
}
