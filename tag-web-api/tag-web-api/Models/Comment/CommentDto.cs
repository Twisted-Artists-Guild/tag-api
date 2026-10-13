namespace TAGWEBAPI.Models;

public class CommentDto
{
    public long Id { get; set; }
    public CommentTargetType TargetType { get; set; }
    public int TargetId { get; set; }
    public int UserId { get; set; }
    public UserInfoDto User { get; set; } = null!;
    public string Content { get; set; } = null!;
    public long? ParentCommentId { get; set; }
    public string? AuthorContextId { get; set; }
    public string AuthorEntityType { get; set; } = "user";
    public int? AuthorEntityId { get; set; }
    public string AuthorDisplayName { get; set; } = null!;
    public string? AuthorImage { get; set; }
    public bool IsEdited { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int ReplyCount { get; set; }
    public List<CommentDto>? Replies { get; set; }
}

public class UserInfoDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Email { get; set; }
    public string? Image { get; set; }
}

public class CreateCommentRequest
{
    public CommentTargetType TargetType { get; set; }
    public int TargetId { get; set; }
    public int UserId { get; set; }
    public string Content { get; set; } = null!;
    public long? ParentCommentId { get; set; }
    public string? AuthorContextId { get; set; }
    public string? AuthorEntityType { get; set; }
    public int? AuthorEntityId { get; set; }
}

public class UpdateCommentRequest
{
    public long CommentId { get; set; }
    public int UserId { get; set; }
    public string Content { get; set; } = null!;
}

public class DeleteCommentRequest
{
    public long CommentId { get; set; }
    public int UserId { get; set; }
}

public class GetCommentsRequest
{
    public CommentTargetType TargetType { get; set; }
    public int TargetId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public bool IncludeReplies { get; set; } = true;
}

public class GetRepliesRequest
{
    public long ParentCommentId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class CommentsResponse
{
    public List<CommentDto> Comments { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}