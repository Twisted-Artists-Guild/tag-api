// <copyright file="UserSettingsConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models.Configurations
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using TAGWEBAPI.Models;

    public class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
    {
        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<UserSettings> builder)
        {
            builder.HasKey(us => us.UserSettingsID);

            builder.HasOne(us => us.User)
                .WithOne(u => u.UserSettings)
                .HasForeignKey<UserSettings>(us => us.UserID)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
