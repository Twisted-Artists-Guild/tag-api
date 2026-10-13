// <copyright file="ArtistCategoryConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TAGWEBAPI.Models.Configurations;

public class ArtistCategoryConfiguration : IEntityTypeConfiguration<ArtistCategory>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ArtistCategory> builder)
    {
        builder.HasKey(ac => ac.ArtistCategoryID);

        builder.Property(ac => ac.Category)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ac => ac.CategoryKey)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(ac => ac.CategoryKey)
            .IsUnique();

        builder.Property(ac => ac.Description)
            .HasMaxLength(255);

        builder.Property(ac => ac.Tags)
            .HasMaxLength(255);

        builder.HasOne(ac => ac.ParentCategory)
            .WithMany(ac => ac.SubCategories)
            .HasForeignKey(ac => ac.ParentArtistCategoryID)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        SeedData(builder);
    }

    private static void SeedData(EntityTypeBuilder<ArtistCategory> builder)
    {
        var categories = new List<ArtistCategory>
        {
            new ArtistCategory
            {
                ArtistCategoryID = 1,
                Category = "Physical Artists",
                CategoryKey = "physical_art",
                Description = "Artists primarily creating tangible works with physical materials and tools.",
                Tags = "artists,physical,tangible",
            },
            new ArtistCategory
            {
                ArtistCategoryID = 2,
                Category = "Digital Artists",
                CategoryKey = "digital_art",
                Description = "Artists primarily creating work with digital tools, software, and technology.",
                Tags = "artists,digital,software",
            },
            new ArtistCategory
            {
                ArtistCategoryID = 3,
                Category = "Event-Based Artists",
                CategoryKey = "event_based_art",
                Description = "Artists whose primary practice is live performance and audience-facing experiences.",
                Tags = "artists,live,performance",
            },
        };

        var categoryKeys = new HashSet<string>(categories.Select(c => c.CategoryKey), StringComparer.OrdinalIgnoreCase);
        var artistTypeToRoot = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Band"] = 3,
            ["Musician"] = 3,
            ["DJ"] = 3,
            ["Music Producer"] = 2,
            ["Audio Artist"] = 2,
            ["Painter"] = 1,
            ["Visual Artist"] = 1,
            ["Mixed Media"] = 1,
            ["Installation Artist"] = 1,
            ["Blacksmith"] = 1,
            ["Woodworker"] = 1,
            ["Ceramicist"] = 1,
            ["Potter"] = 1,
            ["Metalsmith"] = 1,
            ["Glass Artist"] = 1,
            ["Leatherworker"] = 1,
            ["Luthier"] = 1,
            ["Book Arts"] = 1,
            ["Papermaker"] = 1,
            ["Digital Illustrator"] = 2,
            ["Graphic Designer"] = 2,
            ["Motion Designer"] = 2,
            ["Animator"] = 2,
            ["3D Artist"] = 2,
            ["Creative Technologist"] = 2,
            ["AR Artist"] = 2,
            ["VR Artist"] = 2,
            ["Dancer"] = 3,
            ["Choreographer"] = 3,
            ["Theater Performer"] = 3,
            ["Circus Artist"] = 3,
            ["Poet"] = 3,
            ["Writer"] = 3,
            ["Spoken Word Artist"] = 3,
            ["Playwright"] = 3,
        };

        var hierarchyRows = new (string ArtType, string Genre, string SubGenre)[]
        {
            ("Band", "Rock", "Classic Rock"),
            ("Band", "Rock", "Hard Rock"),
            ("Band", "Rock", "Soft Rock"),
            ("Band", "Rock", "Garage Rock"),
            ("Band", "Rock", "Psychedelic Rock"),
            ("Band", "Rock", "Progressive Rock"),
            ("Band", "Rock", "Art Rock"),
            ("Band", "Rock", "Blues Rock"),
            ("Band", "Rock", "Stoner Rock"),
            ("Band", "Rock", "Desert Rock"),
            ("Band", "Punk", "Punk Rock"),
            ("Band", "Punk", "Hardcore Punk"),
            ("Band", "Punk", "Post-Punk"),
            ("Band", "Punk", "Anarcho-Punk"),
            ("Band", "Punk", "Street Punk"),
            ("Band", "Punk", "Crossover Thrash"),
            ("Band", "Punk", "Pop Punk"),
            ("Band", "Alternative", "Alternative Rock"),
            ("Band", "Alternative", "Indie Rock"),
            ("Band", "Alternative", "College Rock"),
            ("Band", "Alternative", "Noise Rock"),
            ("Band", "Alternative", "Post-Rock"),
            ("Band", "Metal", "Heavy Metal"),
            ("Band", "Metal", "Thrash Metal"),
            ("Band", "Metal", "Death Metal"),
            ("Band", "Metal", "Black Metal"),
            ("Band", "Metal", "Doom Metal"),
            ("Band", "Metal", "Sludge Metal"),
            ("Band", "Metal", "Post-Metal"),
            ("Band", "Metal", "Power Metal"),
            ("Band", "Reggae", "Roots Reggae"),
            ("Band", "Reggae", "Dub"),
            ("Band", "Reggae", "Dancehall"),
            ("Band", "Reggae", "Rocksteady"),
            ("Band", "Reggae", "Conscious Reggae"),
            ("Band", "Reggae", "Reggae Fusion"),
            ("Band", "Ska", "Traditional Ska"),
            ("Band", "Ska", "2 Tone Ska"),
            ("Band", "Ska", "Ska Punk"),
            ("Band", "Ska", "Ska Rock"),
            ("Band", "Funk", "Funk Rock"),
            ("Band", "Funk", "P-Funk"),
            ("Band", "Funk", "Neo-Funk"),
            ("Band", "Soul", "Classic Soul"),
            ("Band", "Soul", "Psych Soul"),
            ("Band", "Soul", "Neo-Soul"),
            ("Band", "Blues", "Delta Blues"),
            ("Band", "Blues", "Chicago Blues"),
            ("Band", "Blues", "Electric Blues"),
            ("Band", "Indie", "Indie Folk"),
            ("Band", "Indie", "Indie Pop"),
            ("Band", "Indie", "Lo-Fi Indie"),
            ("Band", "Folk", "Traditional Folk"),
            ("Band", "Folk", "Americana"),
            ("Band", "Folk", "Psych Folk"),
            ("Band", "Jazz", "Jazz Fusion"),
            ("Band", "Jazz", "Avant-Garde Jazz"),
            ("Band", "Jazz", "Free Jazz"),
            ("Band", "World Music", "Afrobeat"),
            ("Band", "World Music", "Latin Rock"),
            ("Band", "World Music", "Cumbia"),
            ("Band", "World Music", "Highlife"),
            ("Musician", "Rock", "Solo Rock"),
            ("Musician", "Jazz", "Improvisational Jazz"),
            ("Musician", "Folk", "Singer-Songwriter"),
            ("Musician", "Classical", "Contemporary Classical"),
            ("DJ", "House", "Deep House"),
            ("DJ", "House", "Tech House"),
            ("DJ", "House", "Minimal House"),
            ("DJ", "House", "Progressive House"),
            ("DJ", "House", "Acid House"),
            ("DJ", "Techno", "Detroit Techno"),
            ("DJ", "Techno", "Minimal Techno"),
            ("DJ", "Techno", "Industrial Techno"),
            ("DJ", "Drum and Bass", "Liquid DnB"),
            ("DJ", "Drum and Bass", "Jungle"),
            ("DJ", "Trance", "Uplifting Trance"),
            ("DJ", "Trance", "Psytrance"),
            ("DJ", "Electronic", "IDM"),
            ("DJ", "Electronic", "Glitch"),
            ("DJ", "Electronic", "Experimental Electronic"),
            ("Music Producer", "Hip-Hop", "Boom Bap"),
            ("Music Producer", "Hip-Hop", "Trap"),
            ("Music Producer", "Hip-Hop", "Drill"),
            ("Music Producer", "Electronic", "Lo-Fi Beats"),
            ("Music Producer", "Electronic", "Ambient Electronic"),
            ("Audio Artist", "Sound Art", "Soundscape"),
            ("Audio Artist", "Sound Art", "Field Recording"),
            ("Audio Artist", "Sound Art", "Noise"),
            ("Audio Artist", "Electro-Acoustic", "Experimental Audio"),
            ("Painter", "Modern Art", "Mid-Century Modern"),
            ("Painter", "Contemporary Art", "Post-Modern"),
            ("Painter", "Abstract", "Geometric Abstraction"),
            ("Painter", "Abstract", "Color Field"),
            ("Painter", "Abstract", "Gestural"),
            ("Painter", "Figurative", "Contemporary Figurative"),
            ("Painter", "Figurative", "Portraiture"),
            ("Painter", "Landscape", "Urban Landscape"),
            ("Painter", "Landscape", "Natural Landscape"),
            ("Painter", "Conceptual Art", "Process-Based"),
            ("Painter", "Conceptual Art", "Site-Specific"),
            ("Visual Artist", "Pop Art", "Neo-Pop"),
            ("Visual Artist", "Street Art", "Graffiti"),
            ("Visual Artist", "Street Art", "Mural-Based"),
            ("Visual Artist", "Lowbrow", "Pop Surrealism"),
            ("Mixed Media", "Conceptual Art", "Assemblage"),
            ("Mixed Media", "Conceptual Art", "Collage"),
            ("Installation Artist", "Environmental", "Immersive"),
            ("Installation Artist", "Environmental", "Interactive"),
            ("Installation Artist", "Social Practice", "Community-Based"),
            ("Blacksmith", "Functional Forge", "Tools"),
            ("Blacksmith", "Functional Forge", "Furniture"),
            ("Blacksmith", "Sculptural Forge", "Abstract Form"),
            ("Blacksmith", "Sculptural Forge", "Architectural"),
            ("Woodworker", "Furniture", "Modern"),
            ("Woodworker", "Furniture", "Rustic"),
            ("Woodworker", "Sculptural", "Carved Forms"),
            ("Ceramicist", "Functional Ware", "Dinnerware"),
            ("Ceramicist", "Functional Ware", "Vessels"),
            ("Ceramicist", "Sculptural Ceramic", "Abstract"),
            ("Potter", "Wheel-Thrown", "Traditional Forms"),
            ("Potter", "Wheel-Thrown", "Contemporary Forms"),
            ("Metalsmith", "Jewelry", "Hand-Fabricated"),
            ("Metalsmith", "Jewelry", "Casting"),
            ("Glass Artist", "Blown Glass", "Functional"),
            ("Glass Artist", "Blown Glass", "Sculptural"),
            ("Glass Artist", "Fused Glass", "Panels"),
            ("Leatherworker", "Leather Goods", "Bags"),
            ("Leatherworker", "Leather Goods", "Footwear"),
            ("Luthier", "String Instruments", "Guitar"),
            ("Luthier", "String Instruments", "Violin"),
            ("Book Arts", "Artist Books", "Conceptual Books"),
            ("Papermaker", "Handmade Paper", "Textured Sheets"),
            ("Papermaker", "Handmade Paper", "Art Paper"),
            ("Digital Illustrator", "Illustration", "Character Design"),
            ("Digital Illustrator", "Illustration", "Editorial Illustration"),
            ("Digital Illustrator", "Illustration", "Concept Art"),
            ("Graphic Designer", "Design", "Brand Identity"),
            ("Graphic Designer", "Design", "Typography"),
            ("Motion Designer", "Motion Graphics", "Kinetic Typography"),
            ("Motion Designer", "Motion Graphics", "Title Design"),
            ("Animator", "Animation", "2D Animation"),
            ("Animator", "Animation", "3D Animation"),
            ("3D Artist", "3D Art", "Stylized 3D"),
            ("3D Artist", "3D Art", "Photorealism"),
            ("Creative Technologist", "Interactive Art", "Sensor-Based"),
            ("Creative Technologist", "Interactive Art", "Installation-Based"),
            ("AR Artist", "Augmented Reality", "Site-Based AR"),
            ("AR Artist", "Augmented Reality", "Mobile AR"),
            ("VR Artist", "Virtual Reality", "Immersive World"),
            ("VR Artist", "Virtual Reality", "Narrative VR"),
            ("Dancer", "Contemporary Dance", "Improvisation"),
            ("Dancer", "Contemporary Dance", "Floor Work"),
            ("Dancer", "Street Dance", "Freestyle"),
            ("Dancer", "Ballet", "Classical Ballet"),
            ("Choreographer", "Contemporary Dance", "Conceptual Choreography"),
            ("Theater Performer", "Experimental Theater", "Devised Performance"),
            ("Theater Performer", "Physical Theater", "Movement-Based"),
            ("Circus Artist", "Contemporary Circus", "Aerial Silks"),
            ("Circus Artist", "Contemporary Circus", "Aerial Hoop"),
            ("Circus Artist", "Flow Arts", "Fire Spinning"),
            ("Circus Artist", "Flow Arts", "LED Performance"),
            ("Poet", "Poetry", "Free Verse"),
            ("Poet", "Poetry", "Spoken Poetry"),
            ("Poet", "Poetry", "Slam Poetry"),
            ("Writer", "Fiction", "Short Stories"),
            ("Writer", "Fiction", "Speculative Fiction"),
            ("Writer", "Nonfiction", "Essays"),
            ("Writer", "Nonfiction", "Memoir"),
            ("Spoken Word Artist", "Performance Poetry", "Narrative Spoken Word"),
            ("Playwright", "Theater Writing", "One-Act Plays"),
            ("Playwright", "Theater Writing", "Full-Length Plays"),
        };

        var nextId = 4;
        var artTypeIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var genreIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in hierarchyRows)
        {
            if (!artistTypeToRoot.TryGetValue(row.ArtType, out var rootId))
            {
                throw new InvalidOperationException($"No root mapping configured for artist type '{row.ArtType}'.");
            }

            if (!artTypeIds.TryGetValue(row.ArtType, out var artTypeId))
            {
                artTypeId = nextId++;
                var artTypeKey = BuildUniqueKey(ToKey(row.ArtType), categoryKeys);
                categories.Add(new ArtistCategory
                {
                    ArtistCategoryID = artTypeId,
                    Category = row.ArtType,
                    CategoryKey = artTypeKey,
                    Description = $"Artists working in {row.ArtType}.",
                    Tags = $"artists,{artTypeKey}",
                    ParentArtistCategoryID = rootId,
                });
                artTypeIds[row.ArtType] = artTypeId;
            }

            var genreLookup = $"{row.ArtType}|{row.Genre}";
            if (!genreIds.TryGetValue(genreLookup, out var genreId))
            {
                genreId = nextId++;
                var genreKey = BuildUniqueKey(ToKey($"{row.ArtType}_{row.Genre}"), categoryKeys);
                categories.Add(new ArtistCategory
                {
                    ArtistCategoryID = genreId,
                    Category = row.Genre,
                    CategoryKey = genreKey,
                    Description = $"{row.ArtType} artists focused on {row.Genre}.",
                    Tags = $"artists,{ToKey(row.ArtType)},{ToKey(row.Genre)}",
                    ParentArtistCategoryID = artTypeId,
                });
                genreIds[genreLookup] = genreId;
            }

            var subGenreKey = BuildUniqueKey(ToKey($"{row.ArtType}_{row.Genre}_{row.SubGenre}"), categoryKeys);
            categories.Add(new ArtistCategory
            {
                ArtistCategoryID = nextId++,
                Category = row.SubGenre,
                CategoryKey = subGenreKey,
                Description = $"{row.ArtType} artists creating {row.SubGenre} within {row.Genre}.",
                Tags = $"artists,{ToKey(row.ArtType)},{ToKey(row.Genre)},{ToKey(row.SubGenre)}",
                ParentArtistCategoryID = genreId,
            });
        }

        builder.HasData(categories.ToArray());
    }

    private static string BuildUniqueKey(string baseKey, HashSet<string> usedKeys)
    {
        var normalized = string.IsNullOrWhiteSpace(baseKey) ? "category" : baseKey;
        normalized = normalized.Length > 50 ? normalized[..50] : normalized;
        if (usedKeys.Add(normalized))
        {
            return normalized;
        }

        var i = 2;
        while (true)
        {
            var suffix = "_" + i;
            var prefixLength = Math.Max(1, 50 - suffix.Length);
            var candidate = (normalized.Length > prefixLength ? normalized[..prefixLength] : normalized) + suffix;
            if (usedKeys.Add(candidate))
            {
                return candidate;
            }

            i++;
        }
    }

    private static string ToKey(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "category";
        }

        var chars = text.Trim().ToLowerInvariant().ToCharArray();
        var buffer = new List<char>(chars.Length);
        var lastWasSeparator = false;

        foreach (var ch in chars)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer.Add(ch);
                lastWasSeparator = false;
                continue;
            }

            if (!lastWasSeparator)
            {
                buffer.Add('_');
                lastWasSeparator = true;
            }
        }

        while (buffer.Count > 0 && buffer[0] == '_')
        {
            buffer.RemoveAt(0);
        }

        while (buffer.Count > 0 && buffer[^1] == '_')
        {
            buffer.RemoveAt(buffer.Count - 1);
        }

        return buffer.Count == 0 ? "category" : new string(buffer.ToArray());
    }
}
