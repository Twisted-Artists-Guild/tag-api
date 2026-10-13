// <copyright file="PictureConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models.Configurations
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using TAGWEBAPI.Models;

    public class PictureConfiguration : IEntityTypeConfiguration<Picture>
    {
        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<Picture> builder)
        {
            builder.HasKey(p => p.PictureID);

            builder.Property(p => p.AltText)
                .HasColumnType("text");

            builder.Property(p => p.ArtistID)
                .IsRequired(false);

            builder.Property(p => p.Context)
                .HasColumnType("text");

            builder.Property(p => p.Created)
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(p => p.Description)
                .HasColumnType("text");

            builder.Property(p => p.EmbedURL)
                .HasColumnType("text");

            builder.Property(p => p.Height)
                .IsRequired(false);

            builder.Property(p => p.Path)
                .HasColumnType("text");

            builder.Property(p => p.ThumbnailHeight)
                .IsRequired(false);

            builder.Property(p => p.ThumbnailURL)
                .HasColumnType("text");

            builder.Property(p => p.ThumbnailWidth)
                .IsRequired(false);

            builder.Property(p => p.Title)
                .HasMaxLength(1000);

            builder.Property(p => p.URL)
                .HasColumnType("text");

            builder.Property(p => p.NormalizedURL)
                .HasColumnType("text");

            builder.HasIndex(p => p.NormalizedURL)
                .IsUnique();

            builder.Property(p => p.Byline)
                .HasMaxLength(2000);

            builder.Property(p => p.UserID)
                .IsRequired(false);

            builder.Property(p => p.Updated)
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(p => p.Width)
                .IsRequired(false);

            SeedData(builder);
        }

        private static void SeedData(EntityTypeBuilder<Picture> builder)
        {
            // Create a consistent UTC timestamp for all seed data
            var creationDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            builder.HasData(
                new Picture { PictureID = 1, Context = "artists", Path = "satarah", Description = "satarah, aerial, fire, circus", Title = "Satarah 1", AltText = "Graphic", URL = "https://tagpictures.blob.core.windows.net/satarah/cropped-saratah-logo-for-web-1-1.png", Height = 80, Width = 150, ArtistID = 4, Created = creationDate, Updated = creationDate },
                new Picture { PictureID = 2, Context = "artists", Path = "satarah", Description = "satarah, aerial, fire, circus", Title = "Satarah 2", AltText = "Forest", URL = "https://tagpictures.blob.core.windows.net/satarah/forest_resized.jpg", ArtistID = 4, Created = creationDate, Updated = creationDate },
                new Picture { PictureID = 3, Context = "artists", Path = "satarah", Description = "satarah, aerial, fire, circus", Title = "Satarah 3", AltText = "Satarah Duo", URL = "https://tagpictures.blob.core.windows.net/satarah/satarah1-5.jpg", ArtistID = 4, Created = creationDate, Updated = creationDate },
                new Picture { PictureID = 4, Context = "artists", Path = "satarah", Description = "satarah, aerial, fire, circus", Title = "Satarah 4", AltText = "Sarah", URL = "https://tagpictures.blob.core.windows.net/satarah/sarah_1.jpg", ArtistID = 4, Created = creationDate, Updated = creationDate },
                new Picture { PictureID = 5, Context = "artists", Path = "twistedpassions", Description = "tie dye", Title = "Logo", AltText = "Logo", URL = "https://tagpictures.blob.core.windows.net/twistedpassions/TP%20Logo.jpg", ArtistID = 1, Created = creationDate, Updated = creationDate },
                new Picture { PictureID = 6, Context = "artists", Path = "twistedpassions", Description = "tie dye", Title = "Logo 2", AltText = "Logo 2", URL = "https://tagpictures.blob.core.windows.net/twistedpassions/TP%20logo%202.jpg", Height = 80, Width = 150, ArtistID = 1, Created = creationDate, Updated = creationDate },
                new Picture { PictureID = 7, Context = "artists", Path = "twistedpassions", Description = "tie dye", Title = "Bobb Shields", AltText = "modeled by Bobb", URL = "https://tagpictures.blob.core.windows.net/twistedpassions/TP%20Bobb%20Model.jpg", ArtistID = 1, Created = creationDate, Updated = creationDate },
                new Picture { PictureID = 8, Context = "artists", Path = "twistedpassions", Description = "tie dye", Title = "Josh Russo", AltText = "modeled by Russo", URL = "https://tagpictures.blob.core.windows.net/twistedpassions/TP%20Josh%20Model.jpg", ArtistID = 1, Created = creationDate, Updated = creationDate },
                new Picture { PictureID = 9, Context = "artists", Path = "twistedpassions", Description = "tie dye", Title = "Thumbs Up!", AltText = "Thumbs Up", URL = "https://tagpictures.blob.core.windows.net/twistedpassions/TP%20thumbs%20up.jpg", ArtistID = 1, Created = creationDate, Updated = creationDate },
                new Picture { PictureID = 10, Context = "artists", Path = "twistedpassions", Description = "tie dye", Title = "Twisted Passions Cover", AltText = "TP Cover photo", URL = "https://tagpictures.blob.core.windows.net/twistedpassions/TP%20Cover.jpg", ArtistID = 1, Created = creationDate, Updated = creationDate },
                new Picture { PictureID = 11, Context = "artists", Path = "satarah", Description = "satarah, aerial, fire, circus", Title = "Satarah Performance", AltText = "Satarah performance photo", URL = "https://tagpictures.blob.core.windows.net/satarah/satarah_2.jpg", ArtistID = 4, Created = creationDate, Updated = creationDate },
                new Picture { PictureID = 12, Context = "artists", Path = "satarah", Description = "satarah, aerial, fire, circus", Title = "Satarah Aerial", AltText = "Satarah aerial performance", URL = "https://tagpictures.blob.core.windows.net/satarah/satarah1-1.webp", ArtistID = 4, Created = creationDate, Updated = creationDate },
                new Picture { PictureID = 13, Context = "artists", Path = "satarah", Description = "satarah, aerial, fire, circus", Title = "Satarah Fire Dance", AltText = "Satarah fire performance", URL = "https://tagpictures.blob.core.windows.net/satarah/satarh_3.webp", ArtistID = 4, Created = creationDate, Updated = creationDate },
                new Picture { PictureID = 14, Context = "artists", Path = "satarah", Description = "satarah, aerial, fire, circus", Title = "Satya Performance", AltText = "Satya performance image", URL = "https://tagpictures.blob.core.windows.net/satarah/satya_4.jpg", ArtistID = 4, Created = creationDate, Updated = creationDate },
                new Picture { PictureID = 15, Context = "artists", Path = "satarah", Description = "satarah, aerial, fire, circus", Title = "Satya", AltText = "Satya portrait", URL = "https://tagpictures.blob.core.windows.net/satarah/satya.jpg", ArtistID = 4, Created = creationDate, Updated = creationDate },
                new Picture { PictureID = 16, Context = "artists", Path = "satarah", Description = "satarah, aerial, fire, circus", Title = "Satarah 1", AltText = "Graphic", URL = "https://tagpictures.blob.core.windows.net/satarah/satarah_cover.png", ArtistID = 4, Created = creationDate, Updated = creationDate },
                new Picture { PictureID = 17, Context = "artists", Path = "campfirecirque", Description = "fire flow, performance art", Title = "Campfire Cirque Logo", AltText = "Campfire Cirque Logo", URL = "https://tagpictures.blob.core.windows.net/campfirecirque/campfirecirque_logo.png", ArtistID = 7, Created = creationDate, Updated = creationDate },
                new Picture { PictureID = 18, Context = "artists", Path = "campfirecirque", Description = "fire flow, performance art", Title = "Campfire Cirque Cover", AltText = "Campfire Cirque Cover photo", URL = "https://tagpictures.blob.core.windows.net/campfirecirque/campfirecirque_cover.png", ArtistID = 7, Created = creationDate, Updated = creationDate }
            );
        }
    }
}
