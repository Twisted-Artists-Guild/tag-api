// <copyright file="ModerationAction.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace TAGWEBAPI.Models;

/// <summary>
/// Records each staff moderation action taken on a report.
/// ActionType: "Block", "Suspend", "RemoveContent", "ChangeTags", "Dismiss", "Escalate", "Note"
/// </summary>
public class ModerationAction
{
    [Key]
    public int ModerationActionID { get; set; }

    public int ContentReportID { get; set; }

    public int StaffID { get; set; }

    /// <summary>
    /// "Block", "Suspend", "RemoveContent", "ChangeTags", "Dismiss", "Escalate", "Note"
    /// </summary>
    [Required]
    [StringLength(50)]
    public string ActionType { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Note { get; set; }

    /// <summary>
    /// JSON metadata for the action (e.g. tags changed, suspension duration).
    /// </summary>
    [StringLength(2000)]
    public string? ActionMetadata { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    [ForeignKey("ContentReportID")]
    public ContentReport ContentReport { get; set; } = null!;

    [ForeignKey("StaffID")]
    public Staff Staff { get; set; } = null!;
}