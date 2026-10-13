// <copyright file="VideoConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations;

public class VideoConfiguration : IEntityTypeConfiguration<Video>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Video> builder)
    {
        builder.HasKey(v => v.VideoID);

        builder.Property(v => v.Title)
            .HasMaxLength(1000);

        builder.Property(v => v.Byline)
            .HasMaxLength(2000);

        builder.Property(v => v.Description)
            .HasColumnType("text");

        builder.Property(v => v.EmbedURL)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(v => v.URL)
            .HasColumnType("text");

        builder.Property(v => v.ThumbnailURL)
            .HasColumnType("text");

        builder.Property(v => v.Provider)
            .HasMaxLength(40)
            .HasDefaultValue("vimeo")
            .IsRequired();

        builder.Property(v => v.ProviderVideoID)
            .HasMaxLength(200);

        builder.Property(v => v.NormalizedEmbedURL)
            .HasColumnType("text");

        builder.HasIndex(v => v.NormalizedEmbedURL);

        builder.Property(v => v.Created)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(v => v.Updated)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_Video_Provider",
                "\"Provider\" IN ('vimeo', 'youtube')");
        });
    }
}