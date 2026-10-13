using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;
[Table("message_impressions", Schema = "public")]
public class MessageImpression
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("message_id")]
    public int MessageId { get; set; }

    [Column("impression_id")]
    public int ImpressionId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Message Message { get; set; } = null!;
    
    // ADD THIS: Navigation property to PrimaryImpression
    [ForeignKey("ImpressionId")]
    public PrimaryImpression Impression { get; set; } = null!;
}
