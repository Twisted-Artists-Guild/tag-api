// <copyright file="ContestConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations;

public class ContestConfiguration : IEntityTypeConfiguration<Contest>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Contest> builder)
    {
        // Primary Key
        builder.HasKey(c => c.Id);

        // Ensure the Slug is unique for SEO-friendly URLs
        builder.HasIndex(c => c.Slug)
            .IsUnique();

        builder.Property(c => c.Title)
            .IsRequired();

        builder.Property(c => c.Prompt)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(c => c.Status)
            .HasDefaultValue("Active")
            .HasMaxLength(20);

        builder.Property(c => c.Period)
            .IsRequired()
            .HasMaxLength(20);

        // Navigation property configuration
        builder.HasMany(c => c.Entries)
            .WithOne(e => e.Contest)
            .HasForeignKey(e => e.ContestId)
            .OnDelete(DeleteBehavior.Cascade);

        SeedData(builder);
    }

    private static void SeedData(EntityTypeBuilder<Contest> builder)
    {
        builder.HasData(
            new Contest
            {
                Id = 1,
                Title = "<h1>Spring Awakening 2026</h1>",
                Slug = "spring-awakening-2026",
                Prompt = "Renewal and Growth",
                Description = "<p>Show us what renewal looks like in the twisted world of art.</p>",
                Guidelines = "<ul><li>One entry per artist</li><li>Must be original work</li></ul>",
                StartDate = new DateTime(2026, 05, 01, 0, 0, 0, DateTimeKind.Utc),
                WarmupEndDate = new DateTime(2026, 05, 15, 23, 59, 59, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 05, 31, 23, 59, 59, DateTimeKind.Utc),
                Period = "Monthly",
                Status = "Active",
                CreatedAt = new DateTime(2026, 04, 15, 10, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}