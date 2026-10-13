// <copyright file="LinkerVendorToUserConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models.Configurations
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using TAGWEBAPI.Models;

    public class Linker_VendorToUserConfiguration : IEntityTypeConfiguration<Linker_VendorToUser>
    {
        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<Linker_VendorToUser> builder)
        {
            builder.HasKey(l => l.Linker_VendorToUserID);

            builder.Property(l => l.Title)
                .HasMaxLength(1000);
        }
    }
}
