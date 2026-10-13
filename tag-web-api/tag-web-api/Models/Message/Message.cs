// <copyright file="Message.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;
[Table("messages", Schema = "public")]
public class Message
{
    [Key]
    [Column("id")]
    public int MessageID { get; set; }

    // Original short content field kept for legacy, but we store protected payload in EncryptedBody
    [NotMapped]
    public string? DirMsg { get; set; }

    // Encrypted payload (server-side protected)
    [Column("encrypted_body")]
    public string? EncryptedBody { get; set; }

    [Column("is_encrypted")]
    public bool IsEncrypted { get; set; } = true;

    [Column("is_edited")]
    public bool Edited { get; set; }

    [Column("from_user_id")]
    public int FromUserID { get; set; }

    [Column("picture_id")]
    public int? PictureID { get; set; }

    [Column("sent_at")]
    public DateTime Sent { get; set; } = DateTime.UtcNow;

    // If part of a conversation; null for legacy single-recipient messages
    [Column("conversation_id")]
    public int? ConversationId { get; set; }

    [Column("to_user_id")]
    public int? ToUserID { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [Column("is_read")]
    public bool IsRead { get; set; }

    // Navigation properties
    public NextAuthUser? FromUser { get; set; }

    public Picture? Picture { get; set; }

    public NextAuthUser? ToUser { get; set; }
    public Conversation? Conversation { get; set; }

    public ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();

    public ICollection<MessageImpression> Impressions { get; set; } = new List<MessageImpression>();
}
