using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace TAGWEBAPI.Models;

[Table("contest_entries", Schema = "public")]
public class ContestEntry
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("contest_id")]
    public int ContestId { get; set; }

    [Column("listing_id")]
    public int ListingId { get; set; }

    [Column("artist_id")]
    public int ArtistId { get; set; }

    [Column("joined_at")]
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("ContestId")]
    [JsonIgnore]
    public virtual Contest Contest { get; set; } = null!;

    [ForeignKey("ListingId")]
    public virtual Listing Listing { get; set; } = null!;

    [ForeignKey("ArtistId")]
    public virtual Artist Artist { get; set; } = null!;
}
