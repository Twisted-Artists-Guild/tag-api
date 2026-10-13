using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

[Table("content_warning_groups", Schema = "public")]
public class ContentWarningGroup
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("title")]
    [Required]
    public string Title { get; set; } = string.Empty;

    [Column("display_order")]
    public int DisplayOrder { get; set; }

    public virtual ICollection<ContentWarningItem> Items { get; set; } = new List<ContentWarningItem>();
}
