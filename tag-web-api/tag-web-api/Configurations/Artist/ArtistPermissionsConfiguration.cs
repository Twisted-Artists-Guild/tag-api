// <copyright file="ArtistPermissionsConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations;

public class ArtistPermissionsConfiguration : IEntityTypeConfiguration<ArtistPermissions>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ArtistPermissions> builder)
    {
        builder.HasKey(ap => ap.ArtistPermissionsID);

        builder.Property(ap => ap.OwnerRole)
            .IsRequired();

        builder.Property(ap => ap.POS_Authorized)
            .IsRequired();

        builder.HasOne(ap => ap.Artist)
            .WithOne(a => a.ArtistPermissions)
            .HasForeignKey<ArtistPermissions>(ap => ap.ArtistID)
            .OnDelete(DeleteBehavior.Cascade);

        SeedData(builder);
    }

    private static void SeedData(EntityTypeBuilder<ArtistPermissions> builder)
    {
        builder.HasData(
            new ArtistPermissions { ArtistPermissionsID = 1, ArtistID = 1, OwnerRole = true, POS_Authorized = true },
            new ArtistPermissions { ArtistPermissionsID = 2, ArtistID = 2, OwnerRole = true, POS_Authorized = true },
            new ArtistPermissions { ArtistPermissionsID = 3, ArtistID = 3, OwnerRole = false, POS_Authorized = false },
            new ArtistPermissions { ArtistPermissionsID = 4, ArtistID = 4, OwnerRole = false, POS_Authorized = false },
            new ArtistPermissions { ArtistPermissionsID = 5, ArtistID = 5, OwnerRole = false, POS_Authorized = false },
            new ArtistPermissions { ArtistPermissionsID = 6, ArtistID = 6, OwnerRole = false, POS_Authorized = false },
            new ArtistPermissions { ArtistPermissionsID = 7, ArtistID = 7, OwnerRole = false, POS_Authorized = false });
    }
}
