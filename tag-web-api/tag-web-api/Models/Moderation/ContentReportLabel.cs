// <copyright file="ContentReportLabel.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace TAGWEBAPI.Models;

/// <summary>
/// Links a content report to the content warning labels the reporter selected.
/// </summary>
public class ContentReportLabel
{
    [Key]
    public int ContentReportLabelID { get; set; }

    public int ContentReportID { get; set; }

    public int ContentWarningItemID { get; set; }

    [JsonIgnore]
    [ForeignKey("ContentReportID")]
    public ContentReport ContentReport { get; set; } = null!;

    [ForeignKey("ContentWarningItemID")]
    public ContentWarningItem ContentWarningItem { get; set; } = null!;
}