// <copyright file="LinkerUserFavoriteConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models.Configurations
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using TAGWEBAPI.Models;

    public class Linker_UserFavoriteConfiguration : IEntityTypeConfiguration<Linker_UserFavorite>
    {
        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<Linker_UserFavorite> builder)
        {
            builder.HasKey(l => l.Linker_UserFavoriteID);

            builder.Property(l => l.Note)
                .HasMaxLength(1000);

            builder.Property(l => l.FavoriteDate)
                .HasDefaultValueSql("now()");

            builder.Property(l => l.ListingID)
                .IsRequired();

            builder.Property(l => l.Order)
                .IsRequired();
        }
    }
}
