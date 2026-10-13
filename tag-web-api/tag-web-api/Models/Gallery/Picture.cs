// <copyright file="Picture.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace TAGWEBAPI.Models;
public class Picture
{
    [Key]
    public int PictureID { get; set; }

    public string? AltText { get; set; }

    public int? ArtistID { get; set; }

    public string? Context { get; set; }

    public DateTime Created { get; set; }

    public string? Description { get; set; }

    public string? EmbedURL { get; set; }

    public int? Height { get; set; }

    public string? Path { get; set; }

    public int? ThumbnailHeight { get; set; }

    public string? ThumbnailURL { get; set; }

    public int? ThumbnailWidth { get; set; }

    public string? Title { get; set; }

    public string? URL { get; set; }

    public string? NormalizedURL { get; set; }

    public string? Byline { get; set; }

    public int? UserID { get; set; }

    public DateTime Updated { get; set; }

    public int? Width { get; set; }
}
