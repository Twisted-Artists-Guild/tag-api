using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

[Table("contests", Schema = "public")]
public class Contest
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Column("slug")]
    public string Slug { get; set; } = string.Empty;

    [Required]
    [Column("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("guidelines")]
    public string? Guidelines { get; set; }

    [Required]
    [Column("start_date")]
    public DateTime StartDate { get; set; }

    [Column("warmup_end_date")]
    public DateTime? WarmupEndDate { get; set; }

    [Required]
    [Column("end_date")]
    public DateTime EndDate { get; set; }

    [Required]
    [Column("period")]
    public string Period { get; set; } = "Monthly";

    [Column("cover_pic_url")]
    public string? CoverPicUrl { get; set; }

    [Column("status")]
    public string Status { get; set; } = "Active";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<ContestEntry> Entries { get; set; } = new List<ContestEntry>();
}