namespace TAGWEBAPI.Models;

public class ReactImpressionRequest
{
    public TargetType TargetType { get; set; }
    public int TargetId { get; set; }
    public int ImpressionId { get; set; }
    public int UserId { get; set; }
}   