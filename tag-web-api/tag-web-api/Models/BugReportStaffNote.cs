// <copyright file="BugReportStaffNote.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models;

public class BugReportStaffNote
{
    public int BugReportStaffNoteID { get; set; }

    public int BugReportID { get; set; }

    public int? StaffID { get; set; }

    public string Note { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public BugReport BugReport { get; set; } = null!;

    public Staff? Staff { get; set; }
}