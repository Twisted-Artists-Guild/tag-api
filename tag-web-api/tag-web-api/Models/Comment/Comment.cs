using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

[Table("comments", Schema = "public")]
public class Comment
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("target_type")]
    public CommentTargetType TargetType { get; set; }

    [Column("target_id")]
    public int TargetId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("content")]
    [MaxLength(2000)]
    public string Content { get; set; } = null!;

    [Column("parent_comment_id")]
    public long? ParentCommentId { get; set; }

    // Profile context the comment was posted as, e.g. "artist-12" (null = base user identity).
    [Column("author_context_id")]
    [MaxLength(100)]
    public string? AuthorContextId { get; set; }

    // Entity type of the selected posting identity, e.g. "artist" (null = base user identity).
    [Column("author_entity_type")]
    [MaxLength(50)]
    public string? AuthorEntityType { get; set; }

    // ID of the selected entity (e.g. ArtistID) within AuthorEntityType.
    [Column("author_entity_id")]
    public int? AuthorEntityId { get; set; }

    [Column("is_edited")]
    public bool IsEdited { get; set; } = false;

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    // Navigation property for replies
    [ForeignKey("ParentCommentId")]
    public Comment? ParentComment { get; set; }

    public ICollection<Comment> Replies { get; set; } = new List<Comment>();
}

public enum CommentTargetType
{
    Artist = 1,
    Listing = 2,
    Blog = 3,
    News = 4,
    User = 5,
    FeedPost = 6
}