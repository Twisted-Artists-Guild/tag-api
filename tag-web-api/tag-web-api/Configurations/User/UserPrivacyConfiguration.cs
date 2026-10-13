// <copyright file="UserPrivacyConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models.Configurations
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using TAGWEBAPI.Models;

    public class UserPrivacyConfiguration : IEntityTypeConfiguration<UserPrivacy>
    {
        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<UserPrivacy> builder)
        {
            builder.HasKey(up => up.UserPrivacyID);

            builder.Property(up => up.HideProfileFromPublic)
                .IsRequired();

            builder.Property(up => up.HidingFrom_NameAndDescription)
                .HasMaxLength(50);

            builder.Property(up => up.HidingFromAbuser)
                .IsRequired();

            builder.HasOne(up => up.User)
                .WithOne(u => u.UserPrivacy)
                .HasForeignKey<UserPrivacy>(up => up.UserID)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
