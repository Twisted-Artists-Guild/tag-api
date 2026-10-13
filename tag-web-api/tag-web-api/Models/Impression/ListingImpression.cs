using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

[Table("listing_impressions", Schema = "public")]
public class ListingImpression
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("listing_id")]
    public int ListingId { get; set; }

    [Column("impression_id")]
    public int ImpressionId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}