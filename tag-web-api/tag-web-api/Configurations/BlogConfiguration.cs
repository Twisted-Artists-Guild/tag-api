// <copyright file="BlogConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

namespace TAGWEBAPI.Models.Configurations;

public class BlogConfiguration : IEntityTypeConfiguration<Blog>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Blog> builder)
    {
        builder.HasKey(b => b.BlogID);

        builder.Property(b => b.Body)
            .IsRequired();

        builder.Property(b => b.Byline)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(b => b.Created)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(b => b.Modified)
            .IsRequired(false)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Save);

        builder.Property(b => b.Path)
            .IsRequired();

        builder.HasIndex(b => b.Path)
            .IsUnique();

        builder.Property(b => b.Title)
            .IsRequired()
            .HasMaxLength(1000);

        builder.HasOne(b => b.User)
            .WithMany()
            .HasForeignKey(b => b.UserID)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(b => b.GalleryID)
            .IsRequired(false);

        builder.HasOne(b => b.Gallery)
            .WithMany()
            .HasForeignKey(b => b.GalleryID)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.Property(b => b.CoverPicID)
            .IsRequired(false);

        builder.HasOne(b => b.CoverPic)
            .WithMany()
            .HasForeignKey(b => b.CoverPicID)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        SeedData(builder);
    }

    private static void SeedData(EntityTypeBuilder<Blog> builder)
    {
        // Use explicit UTC DateTime for seed data
        var creationDate = new DateTime(2024, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        var modifiedDate = new DateTime(2024, 5, 2, 14, 30, 0, DateTimeKind.Utc);
        
        builder.HasData(
            new Blog { 
                BlogID = 1, 
                UserID = 1, 
                Title = "TAG Vision Outline", 
                Byline = "The vision will be shown in the Alpha build", 
                Body = "The Twisted Artists Guild will unite artists and customers in new and innovative ways.", 
                Path = "tag-vision-outline",
                Created = creationDate,
                Modified = modifiedDate
            },
            new Blog { 
                BlogID = 2, 
                UserID = 1, 
                Title = "Initial Development Plan", 
                Byline = "What the website will look like in early stages", 
                Body = "Stuff and things. Theyre interesting. Someone come read me, im lonely", 
                Path = "initial-development-plan",
                Created = creationDate,
                Modified = modifiedDate
            },
            new Blog { 
                BlogID = 3, 
                UserID = 1, 
                Title = "Long Term Plan", 
                Byline = "Its gonna be amazing", 
                Body = "I really dont know what to say here. I can keep typing, but its not really quality stuff now is it. I wonder how hard it will be to make updates to these ramblings!?", 
                Path = "long-term-plan",
                Created = creationDate,
                Modified = modifiedDate
            });
    }
}
