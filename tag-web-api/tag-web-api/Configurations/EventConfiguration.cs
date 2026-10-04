// <copyright file="EventConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Models.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.HasKey(e => e.EventID);

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.Description)
            .IsRequired();

        builder.Property(e => e.StartTime)
            .IsRequired();

        builder.Property(e => e.EndTime)
            .IsRequired();

        builder.Property(e => e.Doors)
            .IsRequired();

        builder.Property(e => e.PointOfContact)
            .HasMaxLength(1000);

        builder.Property(e => e.Note)
            .HasMaxLength(1000);

        builder.Property(e => e.Cost)
            .HasColumnType("decimal(15,2)");

        builder.Property(e => e.MaxOccupancy)
            .IsRequired();

        builder.Property(e => e.MinimumAge)
            .IsRequired();

        builder.Property(e => e.Path)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.EventCategoryID)
            .IsRequired(false);

        builder.Property(e => e.GalleryID)
            .IsRequired(false);

        builder.Property(e => e.CoverPicID)
            .IsRequired(false);

        builder.Property(e => e.ProfilePicID)
            .IsRequired(false);

        builder.Property(e => e.LogoPicID)
            .IsRequired(false);

        builder.HasIndex(e => e.Path)
            .IsUnique();

        // Configure navigation properties with proper relationship
        builder.HasOne(e => e.Venue)
            .WithMany()
            .HasForeignKey(e => e.VenueID)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Artist)
            .WithMany()
            .HasForeignKey(e => e.ArtistID)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.EventCategory)
            .WithMany()
            .HasForeignKey(e => e.EventCategoryID)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Gallery)
            .WithMany()
            .HasForeignKey(e => e.GalleryID)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(e => e.CoverPic)
            .WithMany()
            .HasForeignKey(e => e.CoverPicID)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(e => e.ProfilePic)
            .WithMany()
            .HasForeignKey(e => e.ProfilePicID)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(e => e.LogoPic)
            .WithMany()
            .HasForeignKey(e => e.LogoPicID)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
            
        // Add seed data
        SeedData(builder);
    }
    
    private static void SeedData(EntityTypeBuilder<Event> builder)
    {
        // Static date - June 3, 2025 (exactly one month from now)
        
        // Tie Dye class by Twisted Passions
        builder.HasData(
            new Event 
            { 
                EventID = 1, 
                Title = "Tie Dye Workshop", 
                Description = "Learn how to create beautiful tie dye patterns in this hands-on workshop!", 
                StartTime = new DateTime(2025, 6, 3, 14, 0, 0, DateTimeKind.Utc), // 2:00 PM UTC
                EndTime = new DateTime(2025, 6, 3, 16, 0, 0, DateTimeKind.Utc),   // 4:00 PM UTC
                Doors = new DateTime(2025, 6, 3, 13, 30, 0, DateTimeKind.Utc),    // 1:30 PM UTC
                PointOfContact = "Twisted Passions Team",
                Note = "All materials provided. Wear clothes you don't mind getting dye on!",
                Cost = 35.00M,
                MaxOccupancy = 15,
                MinimumAge = 12,
                Path = "tie-dye-workshop",
                EventCategoryID = 1,
                VenueID = 1,
                ArtistID = 1
            }
        );
        
        // Aerial classes by Studio Satarah
        builder.HasData(
            new Event 
            { 
                EventID = 2, 
                Title = "Beginner Aerial Class", 
                Description = "Introduction to aerial arts. Learn basic climbs, poses, and transitions.", 
                StartTime = new DateTime(2025, 6, 4, 18, 0, 0, DateTimeKind.Utc), // 6:00 PM UTC
                EndTime = new DateTime(2025, 6, 4, 19, 30, 0, DateTimeKind.Utc),  // 7:30 PM UTC
                Doors = new DateTime(2025, 6, 4, 17, 30, 0, DateTimeKind.Utc),    // 5:30 PM UTC
                PointOfContact = "Studio Satarah",
                Note = "Wear fitted athletic clothes. No jewelry. Beginners welcome!",
                Cost = 25.00M,
                MaxOccupancy = 10,
                MinimumAge = 16,
                Path = "beginner-aerial-class",
                EventCategoryID = 2,
                VenueID = 5, // Satarah Studio venue
                ArtistID = 4
            },
            new Event 
            { 
                EventID = 3, 
                Title = "Intermediate Aerial Class", 
                Description = "Build on your aerial foundation with more complex sequences and transitions.", 
                StartTime = new DateTime(2025, 6, 6, 18, 0, 0, DateTimeKind.Utc), // 6:00 PM UTC
                EndTime = new DateTime(2025, 6, 6, 19, 30, 0, DateTimeKind.Utc),  // 7:30 PM UTC
                Doors = new DateTime(2025, 6, 6, 17, 30, 0, DateTimeKind.Utc),    // 5:30 PM UTC
                PointOfContact = "Studio Satarah",
                Note = "Requires at least 3 months of aerial experience. Bring water!",
                Cost = 30.00M,
                MaxOccupancy = 8,
                MinimumAge = 16,
                Path = "intermediate-aerial-class",
                EventCategoryID = 2,
                VenueID = 5, // Satarah Studio venue
                ArtistID = 4
            },
            new Event 
            { 
                EventID = 4, 
                Title = "Advanced Aerial Class", 
                Description = "Master complex aerial techniques, drops, and performance-ready sequences.", 
                StartTime = new DateTime(2025, 6, 8, 18, 0, 0, DateTimeKind.Utc), // 6:00 PM UTC
                EndTime = new DateTime(2025, 6, 8, 19, 30, 0, DateTimeKind.Utc),  // 7:30 PM UTC
                Doors = new DateTime(2025, 6, 8, 17, 30, 0, DateTimeKind.Utc),    // 5:30 PM UTC
                PointOfContact = "Studio Satarah",
                Note = "For experienced aerialists only. Minimum 1 year of consistent training required.",
                Cost = 35.00M,
                MaxOccupancy = 6,
                MinimumAge = 18,
                Path = "advanced-aerial-class",
                EventCategoryID = 2,
                VenueID = 5, // Satarah Studio venue
                ArtistID = 4
            }
        );
    }
}
