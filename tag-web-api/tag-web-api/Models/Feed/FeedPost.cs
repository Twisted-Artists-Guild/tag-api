// <copyright file="FeedPost.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

/// <summary>
/// A social feed post. Can be a general text post, or share a specific entity (listing, artist profile, event, etc.)
/// </summary>
public class FeedPost
{
    [Key]
    public int FeedPostID { get; set; }

    /// <summary>
    /// The user who created the post (always a UserID).
    /// </summary>
    public int AuthorUserID { get; set; }

    /// <summary>
    /// If posted on behalf of an entity: "Artist", "Vendor", "Venue", "Event". Null for personal user posts.
    /// </summary>
    [StringLength(50)]
    public string? AuthorEntityType { get; set; }

    /// <summary>
    /// The entity ID if posting as an entity (e.g. ArtistID). Null for personal posts.
    /// </summary>
    public int? AuthorEntityID { get; set; }

    /// <summary>
    /// "General", "ShareListing", "ShareArtist", "ShareEvent", "ShareVendor", "ShareVenue", "HelloWorld"
    /// </summary>
    [Required]
    [StringLength(50)]
    public string PostType { get; set; } = "General";

    [StringLength(5000)]
    public string? Body { get; set; }

    [StringLength(5000)]
    public string? Body_Plaintext { get; set; }

    /// <summary>
    /// The type of entity being shared: "Listing", "Artist", "Event", "Vendor", "Venue". Null for general posts.
    /// </summary>
    [StringLength(50)]
    public string? SharedEntityType { get; set; }

    /// <summary>
    /// The ID of the shared entity. Null for general posts.
    /// </summary>
    public int? SharedEntityID { get; set; }

    /// <summary>
    /// Optional external share URL or deep link.
    /// </summary>
    [StringLength(500)]
    public string? SharedURL { get; set; }

    public int? PictureID { get; set; }

    public int? GalleryID { get; set; }

    public bool IsPublished { get; set; } = true;

    public bool IsModerationBlocked { get; set; }

    /// <summary>
    /// Whether this post was auto-suggested by the platform (e.g. "Hello World" post).
    /// </summary>
    public bool IsSuggestedPost { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation
    [ForeignKey("AuthorUserID")]
    public User Author { get; set; } = null!;

    [ForeignKey("PictureID")]
    public Picture? Picture { get; set; }

    [ForeignKey("GalleryID")]
    public Gallery? Gallery { get; set; }

    public ICollection<FeedPostImpression> Impressions { get; set; } = new List<FeedPostImpression>();
}