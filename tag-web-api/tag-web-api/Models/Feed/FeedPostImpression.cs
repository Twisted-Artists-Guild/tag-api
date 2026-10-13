// <copyright file="FeedPostImpression.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

[Table("feed_post_impressions", Schema = "public")]
public class FeedPostImpression
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("feed_post_id")]
    public int FeedPostId { get; set; }

    [Column("impression_id")]
    public int ImpressionId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("FeedPostId")]
    public FeedPost FeedPost { get; set; } = null!;

    [ForeignKey("ImpressionId")]
    public PrimaryImpression Impression { get; set; } = null!;
}