using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

[Table("content_warning_items", Schema = "public")]
public class ContentWarningItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("group_id")]
    public int GroupId { get; set; }

    [Column("key_name")]
    [Required]
    public string KeyName { get; set; } = string.Empty;

    [Column("label")]
    [Required]
    public string Label { get; set; } = string.Empty;

    [Column("note")]
    public string? Note { get; set; }

    [Column("policy_type")]
    public string PolicyType { get; set; } = "allowed";

    [Column("default_hidden")]
    public bool DefaultHidden { get; set; }

    [Column("display_order")]
    public int DisplayOrder { get; set; }

    [ForeignKey("GroupId")]
    public virtual ContentWarningGroup Group { get; set; } = null!;
}

