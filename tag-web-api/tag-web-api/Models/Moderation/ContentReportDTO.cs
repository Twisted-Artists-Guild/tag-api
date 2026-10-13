// <copyright file="ContentReportDTO.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models;

public class CreateContentReportDTO
{
    public string TargetType { get; set; } = string.Empty;

    public int TargetID { get; set; }

    public string? TargetURL { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Array of ContentWarningItem IDs the reporter selected.
    /// </summary>
    public List<int> LabelIDs { get; set; } = new();
}

public class ContentReportSummaryDTO
{
    public int ContentReportID { get; set; }

    public string TargetType { get; set; } = string.Empty;

    public int TargetID { get; set; }

    public string? TargetURL { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int Priority { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? ReporterName { get; set; }

    public int ReporterUserID { get; set; }

    public string? AssignedStaffName { get; set; }

    public int? AssignedStaffID { get; set; }

    public string? ResolutionNote { get; set; }

    public List<ContentReportLabelDTO> Labels { get; set; } = new();

    public List<ModerationActionDTO> Actions { get; set; } = new();
}

public class ContentReportLabelDTO
{
    public int ContentWarningItemID { get; set; }

    public string Label { get; set; } = string.Empty;

    public string GroupTitle { get; set; } = string.Empty;
}

public class ModerationActionDTO
{
    public int ModerationActionID { get; set; }

    public string ActionType { get; set; } = string.Empty;

    public string? Note { get; set; }

    public string? ActionMetadata { get; set; }

    public string? StaffName { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? SideEffectSummary { get; set; }
}

public class CreateModerationActionDTO
{
    public int ContentReportID { get; set; }

    public string ActionType { get; set; } = string.Empty;

    public string? Note { get; set; }

    public string? ActionMetadata { get; set; }
}

public class UpdateReportStatusDTO
{
    public string Status { get; set; } = string.Empty;

    public int? AssignedStaffID { get; set; }

    public int? Priority { get; set; }

    public string? ResolutionNote { get; set; }
}