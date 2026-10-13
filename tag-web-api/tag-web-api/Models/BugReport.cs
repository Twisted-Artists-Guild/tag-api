// <copyright file="BugReport.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models;

public class BugReport
{
    public int BugReportID { get; set; }

    public int? UserID { get; set; }

    public string? ReporterEmail { get; set; }

    public string ShortDescription { get; set; } = string.Empty;

    public string ExpectedBehavior { get; set; } = string.Empty;

    public string LongDescription { get; set; } = string.Empty;

    public string? SessionContext { get; set; }

    public string? BuildNumber { get; set; }

    public string? PageContext { get; set; }

    public string? Diagnostics { get; set; }

    public string Status { get; set; } = "new";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }

    public ICollection<BugReportStaffNote> StaffNotes { get; set; } = new List<BugReportStaffNote>();
}