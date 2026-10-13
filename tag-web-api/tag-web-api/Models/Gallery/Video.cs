// <copyright file="Video.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace TAGWEBAPI.Models;

public class Video
{
    [Key]
    public int VideoID { get; set; }

    public int? UserID { get; set; }

    public int? ArtistID { get; set; }

    public string? Title { get; set; }

    public string? Byline { get; set; }

    public string? Description { get; set; }

    public string EmbedURL { get; set; } = string.Empty;

    public string? URL { get; set; }

    public string? ThumbnailURL { get; set; }

    public string Provider { get; set; } = "vimeo";

    public string? ProviderVideoID { get; set; }

    public string? NormalizedEmbedURL { get; set; }

    public DateTime Created { get; set; }

    public DateTime Updated { get; set; }
}