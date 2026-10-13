// <copyright file="UserPreferenceConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models.Configurations
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using TAGWEBAPI.Models;

    public class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
    {
        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<UserPreference> builder)
        {
            builder.HasKey(up => up.UserPreferenceID);

            builder.Property(up => up.MetricOrImperial)
                .HasMaxLength(50);

            builder.Property(up => up.ThemePreference)
                .HasMaxLength(50);

            builder.HasOne(up => up.User)
                .WithOne(u => u.UserPreference)
                .HasForeignKey<UserPreference>(up => up.UserID)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
