using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

[Table("user_content_preferences", Schema = "public")]
public class UserContentPreference
{
    [Column("user_id")]
    public int UserId { get; set; }

    [Column("item_id")]
    public int ItemId { get; set; }

    [Column("preference_mode")]
    public string PreferenceMode { get; set; } = "alwaysShow";

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

