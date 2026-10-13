using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;
[Table("message_attachments", Schema = "public")]
public class MessageAttachment
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("message_id")]
    public int MessageId { get; set; }

    [Column("file_name")]
    public string FileName { get; set; } = null!;

    [Column("content_type")]
    public string ContentType { get; set; } = null!;

    [Column("url")]
    public string Url { get; set; } = null!;

    [Column("size")]
    public long Size { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Message Message { get; set; } = null!;
}
