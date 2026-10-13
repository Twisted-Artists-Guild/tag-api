using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;
[Table("conversation_participants", Schema = "public")]
public class ConversationParticipant
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("conversation_id")]
    public int ConversationId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("joined_at")]
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    [Column("last_read_at")]
    public DateTime? LastReadAt { get; set; }

    public Conversation Conversation { get; set; } = null!;
}
