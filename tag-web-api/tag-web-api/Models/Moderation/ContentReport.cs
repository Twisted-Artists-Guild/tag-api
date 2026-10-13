// <copyright file="ContentReport.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

public class ContentReport
{
    [Key]
    public int ContentReportID { get; set; }

    [Required]
    public int ReporterUserID { get; set; }

    /// <summary>
    /// The type of content being reported.
    /// Valid values: "Listing", "Artist", "User", "Comment", "Message", "Blog", "Event"
    /// </summary>
    [Required]
    [StringLength(50)]
    public string TargetType { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the reported content (e.g. ListingID, ArtistID, CommentID).
    /// </summary>
    public int TargetID { get; set; }

    /// <summary>
    /// Optional URL/path where the content was seen.
    /// </summary>
    [StringLength(500)]
    public string? TargetURL { get; set; }

    /// <summary>
    /// Free-text description of the complaint.
    /// </summary>
    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Status: "New", "UnderReview", "Resolved", "Dismissed"
    /// </summary>
    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "New";

    /// <summary>
    /// Priority: 0=Low, 1=Normal, 2=High, 3=Critical
    /// </summary>
    public int Priority { get; set; } = 1;

    public int? AssignedStaffID { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(1000)]
    public string? ResolutionNote { get; set; }

    // Navigation
    [ForeignKey("ReporterUserID")]
    public User Reporter { get; set; } = null!;

    [ForeignKey("AssignedStaffID")]
    public Staff? AssignedStaff { get; set; }

    public ICollection<ContentReportLabel> Labels { get; set; } = new List<ContentReportLabel>();

    public ICollection<ModerationAction> Actions { get; set; } = new List<ModerationAction>();
}