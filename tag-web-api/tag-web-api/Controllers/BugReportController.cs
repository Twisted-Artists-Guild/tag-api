// <copyright file="BugReportController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using System.Text.Json;
    using TAGWEBAPI.Data;
    using TAGWEBAPI.Models;

    [Route("api/bug-report")]
    [ApiController]
    [Authorize]
    public class BugReportController : ControllerBase
    {
        private static readonly string[] AllowedStatuses = new[] { "new", "triaged", "in-progress", "resolved", "closed" };
        private readonly TAGDBContext context;

        public BugReportController(TAGDBContext context)
        {
            this.context = context;
        }

        [HttpGet]
        public async Task<ActionResult<object>> GetBugReports()
        {
            var rows = await this.context.BugReports
                .OrderByDescending(report => report.CreatedAt)
                .Select(report => new
                {
                    id = report.BugReportID,
                    createdAt = report.CreatedAt,
                    updatedAt = report.UpdatedAt,
                    status = report.Status,
                    shortDescription = report.ShortDescription,
                    reporterEmail = report.ReporterEmail,
                    pageContext = report.PageContext,
                    buildNumber = report.BuildNumber,
                    staffNotesCount = report.StaffNotes.Count,
                })
                .ToListAsync()
                .ConfigureAwait(false);

            var items = rows.Select(row => new
            {
                id = row.id,
                createdAt = row.createdAt,
                updatedAt = row.updatedAt,
                status = row.status,
                shortDescription = row.shortDescription,
                reporterEmail = row.reporterEmail,
                path = ExtractPath(row.pageContext),
                buildNumber = row.buildNumber,
                staffNotesCount = row.staffNotesCount,
            });

            return this.Ok(new { items });
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<object>> GetBugReport(int id)
        {
            var report = await this.context.BugReports
                .Include(candidate => candidate.StaffNotes)
                    .ThenInclude(candidate => candidate.Staff)
                    .ThenInclude(candidate => candidate.User)
                .FirstOrDefaultAsync(candidate => candidate.BugReportID == id)
                .ConfigureAwait(false);

            if (report == null)
            {
                return this.NotFound(new { error = "Bug report not found" });
            }

            return this.Ok(new { item = MapToDetail(report) });
        }

        [HttpPost]
        public async Task<ActionResult<object>> PostBugReport([FromBody] BugReportCreateRequest request)
        {
            if (request == null)
            {
                return this.BadRequest(new { error = "Request body is required" });
            }

            var shortDescription = CleanText(request.ShortDescription, 180);
            var expectedBehavior = CleanText(request.ExpectedBehavior, 1200);
            var longDescription = CleanText(request.LongDescription, 4000);
            var reporterEmail = CleanText(request.Email, 250);
            var buildNumber = CleanText(request.BuildNumber, 200);

            if (string.IsNullOrWhiteSpace(shortDescription) || string.IsNullOrWhiteSpace(expectedBehavior) || string.IsNullOrWhiteSpace(longDescription))
            {
                return this.BadRequest(new { error = "Short description, expected behavior, and detailed description are required" });
            }

            var report = new BugReport
            {
                UserID = ParseUserIdFromSessionContext(request.SessionContext),
                ReporterEmail = reporterEmail,
                ShortDescription = shortDescription,
                ExpectedBehavior = expectedBehavior,
                LongDescription = longDescription,
                SessionContext = SerializeJson(request.SessionContext),
                BuildNumber = buildNumber,
                PageContext = SerializeJson(request.PageContext),
                Diagnostics = SerializeJson((request.Diagnostics ?? new List<object>()).TakeLast(40)),
                Status = "new",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            this.context.BugReports.Add(report);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.CreatedAtAction(nameof(GetBugReport), new { id = report.BugReportID }, new { id = report.BugReportID });
        }

        [HttpPatch("{id:int}")]
        public async Task<ActionResult<object>> PatchBugReport(int id, [FromBody] BugReportUpdateRequest request)
        {
            if (request == null)
            {
                return this.BadRequest(new { error = "Request body is required" });
            }

            var report = await this.context.BugReports
                .Include(candidate => candidate.StaffNotes)
                    .ThenInclude(candidate => candidate.Staff)
                    .ThenInclude(candidate => candidate.User)
                .FirstOrDefaultAsync(candidate => candidate.BugReportID == id)
                .ConfigureAwait(false);

            if (report == null)
            {
                return this.NotFound(new { error = "Bug report not found" });
            }

            var note = CleanText(request.Note, 4000);
            var status = CleanText(request.Status, 50).ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(note) && string.IsNullOrWhiteSpace(status))
            {
                return this.BadRequest(new { error = "Provide note and/or status" });
            }

            if (!string.IsNullOrWhiteSpace(status) && !AllowedStatuses.Contains(status, StringComparer.Ordinal))
            {
                return this.BadRequest(new { error = "Invalid status", allowedStatuses = AllowedStatuses });
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                report.Status = status;
            }

            if (!string.IsNullOrWhiteSpace(note))
            {
                var userId = ParseInt(request.Author?.Id);
                var staffId = await this.context.Staffs
                    .Where(candidate => userId.HasValue && candidate.UserID == userId.Value)
                    .OrderByDescending(candidate => candidate.Active)
                    .ThenBy(candidate => candidate.StaffID)
                    .Select(candidate => (int?)candidate.StaffID)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                this.context.BugReportStaffNotes.Add(new BugReportStaffNote
                {
                    BugReportID = report.BugReportID,
                    StaffID = staffId,
                    Note = note,
                    CreatedAt = DateTime.UtcNow,
                });
            }

            report.UpdatedAt = DateTime.UtcNow;
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            var refreshed = await this.context.BugReports
                .Include(candidate => candidate.StaffNotes)
                    .ThenInclude(candidate => candidate.Staff)
                    .ThenInclude(candidate => candidate.User)
                .FirstAsync(candidate => candidate.BugReportID == id)
                .ConfigureAwait(false);

            return this.Ok(new { item = MapToDetail(refreshed) });
        }

        private static object MapToDetail(BugReport report)
        {
            return new
            {
                id = report.BugReportID,
                createdAt = report.CreatedAt,
                updatedAt = report.UpdatedAt,
                status = report.Status,
                shortDescription = report.ShortDescription,
                expectedBehavior = report.ExpectedBehavior,
                longDescription = report.LongDescription,
                reporterEmail = report.ReporterEmail,
                sessionContext = DeserializeJsonObject(report.SessionContext),
                buildNumber = report.BuildNumber,
                pageContext = DeserializeJsonObject(report.PageContext),
                diagnostics = DeserializeJsonArray(report.Diagnostics),
                staffNotes = report.StaffNotes
                    .OrderBy(note => note.CreatedAt)
                    .Select(note => new
                    {
                        noteId = note.BugReportStaffNoteID.ToString(),
                        note = note.Note,
                        createdAt = note.CreatedAt,
                        author = new
                        {
                            id = note.Staff?.UserID.ToString() ?? string.Empty,
                            name = BuildAuthorName(note.Staff?.User),
                            email = note.Staff?.User?.EmailOne,
                            roles = BuildAuthorRoles(note.Staff),
                        },
                    }),
            };
        }

        private static string BuildAuthorName(User? user)
        {
            var preferredName = user?.PreferredName?.Trim();
            if (!string.IsNullOrWhiteSpace(preferredName))
            {
                return preferredName;
            }

            var firstName = user?.FirstName?.Trim();
            var familyName = user?.FamName?.Trim();
            var combinedName = string.Join(" ", new[] { firstName, familyName }.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
            if (!string.IsNullOrWhiteSpace(combinedName))
            {
                return combinedName;
            }

            return user?.Username ?? "staff";
        }

        private static IEnumerable<string> BuildAuthorRoles(Staff? staff)
        {
            return staff == null ? Array.Empty<string>() : new[] { "staff" };
        }

        private static int? ParseUserIdFromSessionContext(object? sessionContext)
        {
            if (sessionContext == null)
            {
                return null;
            }

            if (sessionContext is JsonElement sessionElement && sessionElement.ValueKind == JsonValueKind.Object)
            {
                if (sessionElement.TryGetProperty("userId", out var userIdElement))
                {
                    if (userIdElement.ValueKind == JsonValueKind.Number && userIdElement.TryGetInt32(out var numericUserId))
                    {
                        return numericUserId;
                    }

                    if (userIdElement.ValueKind == JsonValueKind.String)
                    {
                        return ParseInt(userIdElement.GetString());
                    }
                }
            }

            return null;
        }

        private static int? ParseInt(string? value)
        {
            return int.TryParse(value, out var parsed) ? parsed : null;
        }

        private static string SerializeJson(object? value)
        {
            if (value == null)
            {
                return "{}";
            }

            try
            {
                return JsonSerializer.Serialize(value);
            }
            catch
            {
                return "{}";
            }
        }

        private static object DeserializeJsonObject(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new { };
            }

            try
            {
                return JsonSerializer.Deserialize<object>(raw) ?? new { };
            }
            catch
            {
                return new { };
            }
        }

        private static IEnumerable<object> DeserializeJsonArray(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<object>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<object>>(raw) ?? new List<object>();
            }
            catch
            {
                return Array.Empty<object>();
            }
        }

        private static string? ExtractPath(string? pageContextRaw)
        {
            if (string.IsNullOrWhiteSpace(pageContextRaw))
            {
                return string.Empty;
            }

            try
            {
                using var document = JsonDocument.Parse(pageContextRaw);
                if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("path", out var pathElement))
                {
                    return pathElement.ValueKind == JsonValueKind.String ? pathElement.GetString() : string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private static string CleanText(string? value, int maxLen)
        {
            var text = (value ?? string.Empty)
                .Replace("<", string.Empty, StringComparison.Ordinal)
                .Replace(">", string.Empty, StringComparison.Ordinal)
                .Trim();

            return text.Length > maxLen ? text.Substring(0, maxLen) : text;
        }

        public class BugReportCreateRequest
        {
            public string? Email { get; set; }

            public string? ShortDescription { get; set; }

            public string? ExpectedBehavior { get; set; }

            public string? LongDescription { get; set; }

            public string? BuildNumber { get; set; }

            public object? SessionContext { get; set; }

            public object? PageContext { get; set; }

            public List<object>? Diagnostics { get; set; }
        }

        public class BugReportUpdateRequest
        {
            public string? Note { get; set; }

            public string? Status { get; set; }

            public BugReportAuthor? Author { get; set; }
        }

        public class BugReportAuthor
        {
            public string? Id { get; set; }

            public string? Name { get; set; }

            public string? Email { get; set; }

            public List<string>? Roles { get; set; }
        }
    }
}
