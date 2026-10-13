// <copyright file="UserDetailsConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

namespace TAGWEBAPI.Models.Configurations;

public class UserDetailsConfiguration : IEntityTypeConfiguration<User>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.UserID);

        builder.Property(u => u.EmailOne)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(u => u.EmailTwo)
            .HasMaxLength(255);

        builder.Property(u => u.FirstName)
            .HasMaxLength(255);

        builder.Property(u => u.FamName)
            .HasMaxLength(255);

        builder.Property(u => u.Username)
            .HasMaxLength(255)
            .IsRequired();

        builder.HasIndex(u => u.Username)
            .IsUnique();

        builder.Property(u => u.BannedReason)
            .HasColumnType("text");

        builder.Property(u => u.CompanyName)
            .HasMaxLength(255);

        builder.Property(u => u.CompanyTitle)
            .HasMaxLength(255);

        builder.Property(u => u.Nationality)
            .HasMaxLength(255);

        builder.Property(u => u.PreferredName)
            .HasMaxLength(255);

        builder.Property(u => u.Gender)
            .HasMaxLength(255);

        builder.Property(u => u.IsPublished)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.IsModerationBlocked)
            .IsRequired()
            .HasDefaultValue(false);

        // Use CURRENT_TIMESTAMP which will use the server's timezone
        builder.Property(u => u.Joined)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
            
        // Define relationship with Transactions
        builder.HasMany(u => u.Transactions)
            .WithOne(t => t.User)
            .HasForeignKey(t => t.UserID)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(u => u.GalleryID)
            .IsRequired(false);

        builder.Property(u => u.CoverPicID)
            .IsRequired(false);

        builder.Property(u => u.ProfilePicID)
            .IsRequired(false);

        builder.HasOne(u => u.Gallery)
            .WithMany()
            .HasForeignKey(u => u.GalleryID)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(u => u.CoverPic)
            .WithMany()
            .HasForeignKey(u => u.CoverPicID)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(u => u.ProfilePic)
            .WithMany()
            .HasForeignKey(u => u.ProfilePicID)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        SeedData(builder);
    }

    private static void SeedData(EntityTypeBuilder<User> builder)
    {
        // Define a fixed UTC datetime for consistent seed data
        var seedJoinDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        builder.HasData(
            new User { UserID = 1, Username = "spacewizardalpha", FirstName = "Bobb", FamName = "Shields", CompanyName = "Twisted Artists Guild", CompanyTitle = "Executive Chair", EmailOne = "bobbshields@gmail.com", Nationality = "American", Joined = seedJoinDate },
            new User { UserID = 2, Username = "cottonkandy", FirstName = "Emily", FamName = "Barfield", CompanyName = "Art by Em", CompanyTitle = "Artist", EmailOne = "em@bar.com", Nationality = "American", Joined = seedJoinDate },
            new User { UserID = 3, Username = "satya", FirstName = "Katie", FamName = "Rothweiler", CompanyName = "Satarah", CompanyTitle = "Dir: Social Media", EmailOne = "satarah@nowhere.com", Nationality = "American", Joined = seedJoinDate },
            new User { UserID = 4, Username = "vinny", FirstName = "Vincent", FamName = "Ashley", CompanyName = "Saltwater Slide", CompanyTitle = "Marketing and Keyboardist", EmailOne = "sws_vince@nowhere.com", Nationality = "American", Joined = seedJoinDate },
            new User { UserID = 5, Username = "kathleenbug", FirstName = "Kathleen", FamName = "McNamara", CompanyName = "Queen City Cirque", CompanyTitle = "Dir: Artist Relations", EmailOne = "kathleenMcnamara@maybe.com", Nationality = "American", Joined = seedJoinDate },
            new User { UserID = 6, Username = "QCC_Sarah", FirstName = "Sarah", FamName = "Yes", CompanyName = "Queen City Cirque", CompanyTitle = "Dir2: Artist Relations", EmailOne = "sarah@maybe.com", Nationality = "American", Joined = seedJoinDate });
    }
}
