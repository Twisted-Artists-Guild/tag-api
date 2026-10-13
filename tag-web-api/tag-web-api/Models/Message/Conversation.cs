using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;
[Table("conversations", Schema = "public")]
public class Conversation
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("title")]
    [MaxLength(250)]
    public string? Title { get; set; }

    // Whether this is a group conversation
    [Column("is_group")]
    public bool IsGroup { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Last message quick info
    [Column("last_message_at")]
    public DateTime? LastMessageAt { get; set; }

    public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
