// <copyright file="FeedPostDTO.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models;

public class CreateFeedPostDTO
{
    public string? AuthorEntityType { get; set; }

    public int? AuthorEntityID { get; set; }

    public string PostType { get; set; } = "General";

    public string? Body { get; set; }

    public string? SharedEntityType { get; set; }

    public int? SharedEntityID { get; set; }

    public int? PictureID { get; set; }
}

public class FeedPostSummaryDTO
{
    public int FeedPostID { get; set; }

    public int AuthorUserID { get; set; }

    public string? AuthorName { get; set; }

    public string? AuthorImage { get; set; }

    public string? AuthorEntityType { get; set; }

    public int? AuthorEntityID { get; set; }

    public string? AuthorEntityName { get; set; }

    public string? AuthorEntityPath { get; set; }

    public string PostType { get; set; } = string.Empty;

    public string? Body { get; set; }

    public string? Body_Plaintext { get; set; }

    public string? SharedEntityType { get; set; }

    public int? SharedEntityID { get; set; }

    public string? SharedURL { get; set; }

    public object? SharedEntityPreview { get; set; }

    public int? PictureID { get; set; }

    public string? PictureURL { get; set; }

    public bool IsSuggestedPost { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CommentCount { get; set; }

    public int ReactionCount { get; set; }
}

public class SuggestHelloWorldDTO
{
    public string? EntityType { get; set; }

    public int? EntityID { get; set; }
}