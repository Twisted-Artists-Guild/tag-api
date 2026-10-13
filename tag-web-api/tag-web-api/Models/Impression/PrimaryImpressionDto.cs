namespace TAGWEBAPI.Models;

public class PrimaryImpressionDto
{
    public int Id { get; set; }
    public string Emoji { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Label { get; set; } = null!;
    public int DisplayOrder { get; set; }
    public int Count { get; set; }
}

public class PrimaryImpressionRequest
{
    public TargetType TargetType { get; set; }
    public int TargetId { get; set; }
}

public enum TargetType
{
    Listing = 1,
    Artist = 2,
    Comment = 3,
    Blog = 4,
    Message = 5,
    FeedPost = 6
}