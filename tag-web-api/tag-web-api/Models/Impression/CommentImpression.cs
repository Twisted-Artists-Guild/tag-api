using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

[Table("comment_impressions", Schema = "public")]
public class CommentImpression
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("comment_id")]
    public long CommentId { get; set; }

    [Column("impression_id")]
    public int ImpressionId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}