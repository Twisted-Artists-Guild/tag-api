// <copyright file="UserDetailsController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers;

[ApiController]
[Route("api/user-details")]
public class UserDetailsController : ControllerBase
{
    private readonly TAGDBContext context;
    private readonly ILogger<UserDetailsController> logger;
    private static readonly Regex ValidUsernameRegex = new(@"^[a-z0-9](?:[a-z0-9\-]*[a-z0-9])?$", RegexOptions.Compiled);

    public UserDetailsController(TAGDBContext context, ILogger<UserDetailsController> logger)
    {
        this.context = context;
        this.logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserPublicSummaryResponse>>> Get()
    {
        var users = await this.PublicUsersQuery()
            .ToListAsync()
            .ConfigureAwait(false);

        return this.Ok(users.Select(BuildPublicSummary));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserPublicSummaryResponse>> Get(int id)
    {
        var user = await this.PublicUsersQuery()
            .FirstOrDefaultAsync(u => u.UserID == id)
            .ConfigureAwait(false);

        if (user == null)
        {
            return this.NotFound();
        }

        return this.Ok(BuildPublicSummary(user));
    }

    [HttpGet("by-username/{username}")]
    public async Task<ActionResult<UserPublicSummaryResponse>> GetByUsername(string username)
    {
        var normalizedUsername = NormalizeUsername(username);
        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            return this.BadRequest(new { message = "Username is required." });
        }

        var user = await this.PublicUsersQuery()
            .FirstOrDefaultAsync(u => u.Username != null && u.Username.ToLower() == normalizedUsername)
            .ConfigureAwait(false);

        if (user == null)
        {
            return this.NotFound();
        }

        return this.Ok(BuildPublicSummary(user));
    }

    [HttpGet("{id}/private")]
    public async Task<ActionResult<UserPrivateDetailsResponse>> GetPrivate(int id, [FromQuery] int viewerUserId)
    {
        var user = await this.context.Set<User>().FindAsync(id).ConfigureAwait(false);
        if (user == null)
        {
            return this.NotFound();
        }

        if (!await this.HasPrivilegedDetailsAccessAsync(id, viewerUserId).ConfigureAwait(false))
        {
            return this.Forbid();
        }

        return this.Ok(BuildPrivateDetails(user));
    }

    [HttpGet("by-username/{username}/private")]
    public async Task<ActionResult<UserPrivateDetailsResponse>> GetPrivateByUsername(string username, [FromQuery] int viewerUserId)
    {
        var normalizedUsername = NormalizeUsername(username);
        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            return this.BadRequest(new { message = "Username is required." });
        }

        var user = await this.context.Set<User>()
            .FirstOrDefaultAsync(u => u.Username != null && u.Username.ToLower() == normalizedUsername)
            .ConfigureAwait(false);

        if (user == null)
        {
            return this.NotFound();
        }

        if (!await this.HasPrivilegedDetailsAccessAsync(user.UserID, viewerUserId).ConfigureAwait(false))
        {
            return this.Forbid();
        }

        return this.Ok(BuildPrivateDetails(user));
    }

    [HttpGet("admin/unpublished")]
    public async Task<ActionResult<IEnumerable<User>>> GetUnpublished([FromQuery] int moderatorUserId)
    {
        if (!await this.IsModeratorAsync(moderatorUserId).ConfigureAwait(false))
        {
            return this.Forbid();
        }

        var users = await this.context.Set<User>()
            .Where(u => !u.IsPublished || u.IsModerationBlocked)
            .ToListAsync()
            .ConfigureAwait(false);

        return this.Ok(users);
    }

    [HttpPost]
    public async Task<ActionResult<User>> Create(User user)
    {
        this.context.Set<User>().Add(user);
        await this.context.SaveChangesAsync().ConfigureAwait(false);

        await this.CreateDefaultPreferenceRowsAsync(user.UserID).ConfigureAwait(false);

        await this.TryWriteAuditLogAsync(
            shortText: "User created",
            tags: "scope=audit;entity=user;event=profile;operation=create;result=success;channel=db",
            userId: user.UserID,
            loggedData: $"userId={user.UserID};username={user.Username}")
            .ConfigureAwait(false);

        return this.CreatedAtAction(nameof(this.Get), new { id = user.UserID }, user);
    }

    [HttpGet("check-username/{username}")]
    public async Task<ActionResult<object>> CheckUsername(string username, [FromQuery] int? excludeId = null)
    {
        var normalizedUsername = NormalizeUsername(username);

        if (!ValidUsernameRegex.IsMatch(normalizedUsername))
        {
            return this.BadRequest(new { available = false, message = "Invalid username format." });
        }

        var available = await this.IsUsernameUniqueAsync(normalizedUsername, excludeId).ConfigureAwait(false);
        if (!available)
        {
            return this.Conflict(new { available = false, message = "Username is already in use." });
        }

        return this.Ok(new { available = true, username = normalizedUsername });
    }

    [HttpPost("reserve-username")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserUsernameReservationResponse>> ReserveUsername([FromBody] UserUsernameReservationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return this.BadRequest(ModelState);
        }

        var normalizedUsername = NormalizeUsername(request.Username);
        if (!ValidUsernameRegex.IsMatch(normalizedUsername))
        {
            return this.BadRequest("Invalid username format.");
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return this.BadRequest("Email is required.");
        }

        var authUser = await this.context.NextAuthUsers
            .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalizedEmail)
            .ConfigureAwait(false);

        if (authUser == null)
        {
            return this.BadRequest("No NextAuth user found for the provided email.");
        }

        if (!await this.IsUsernameUniqueAsync(normalizedUsername, authUser.Id).ConfigureAwait(false))
        {
            return this.Conflict("Username is already in use.");
        }

        var user = await this.context.Users
            .FirstOrDefaultAsync(u => u.UserID == authUser.Id)
            .ConfigureAwait(false);

        if (user == null)
        {
            user = new User
            {
                UserID = authUser.Id,
                Username = normalizedUsername,
                PreferredName = string.IsNullOrWhiteSpace(request.Title) ? normalizedUsername : request.Title.Trim(),
                EmailOne = normalizedEmail,
                Joined = DateTime.UtcNow,
            };

            this.context.Set<User>().Add(user);
            await this.context.SaveChangesAsync().ConfigureAwait(false);
            await this.CreateDefaultPreferenceRowsAsync(user.UserID).ConfigureAwait(false);

            await this.TryWriteAuditLogAsync(
                shortText: "User reserved by username",
                tags: "scope=audit;entity=user;event=username;operation=reserve;result=success;channel=db",
                userId: user.UserID,
                loggedData: $"userId={user.UserID};username={user.Username}")
                .ConfigureAwait(false);
        }
        else
        {
            user.Username = normalizedUsername;
            user.PreferredName = string.IsNullOrWhiteSpace(request.Title) ? user.PreferredName : request.Title.Trim();
            user.EmailOne = normalizedEmail;
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            await this.TryWriteAuditLogAsync(
                shortText: "User username reserved (existing user updated)",
                tags: "scope=audit;entity=user;event=username;operation=reserve;result=success;channel=db",
                userId: user.UserID,
                loggedData: $"userId={user.UserID};username={user.Username}")
                .ConfigureAwait(false);
        }

        var response = new UserUsernameReservationResponse
        {
            UserID = user.UserID,
            Username = user.Username,
            Title = user.PreferredName,
            Email = user.EmailOne,
        };

        return this.CreatedAtAction(nameof(this.Get), new { id = user.UserID }, response);
    }

    [HttpPatch("{id}/update-username")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserUsernameReservationResponse>> UpdateUsername(int id, [FromBody] UserUsernameUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return this.BadRequest(ModelState);
        }

        var authUserExists = await this.context.NextAuthUsers
            .AnyAsync(u => u.Id == id)
            .ConfigureAwait(false);

        if (!authUserExists)
        {
            return this.NotFound(new { message = $"NextAuth user with ID {id} not found" });
        }

        var user = await this.context.Set<User>().FindAsync(id).ConfigureAwait(false);
        if (user == null)
        {
            return this.NotFound(new { message = $"User with ID {id} not found" });
        }

        var normalizedUsername = NormalizeUsername(request.Username);
        if (!ValidUsernameRegex.IsMatch(normalizedUsername))
        {
            return this.BadRequest("Invalid username format.");
        }

        if (!await this.IsUsernameUniqueAsync(normalizedUsername, id).ConfigureAwait(false))
        {
            return this.Conflict("Username is already in use.");
        }

        user.Username = normalizedUsername;
        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            user.PreferredName = request.Title.Trim();
        }

        await this.context.SaveChangesAsync().ConfigureAwait(false);

        await this.TryWriteAuditLogAsync(
            shortText: "User username updated",
            tags: "scope=audit;entity=user;event=username;operation=update;result=success;channel=db",
            userId: user.UserID,
            loggedData: $"userId={user.UserID};username={user.Username}")
            .ConfigureAwait(false);

        return this.Ok(new UserUsernameReservationResponse
        {
            UserID = user.UserID,
            Username = user.Username,
            Title = user.PreferredName,
            Email = user.EmailOne,
        });
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> Update(int id, [FromBody] UserDetailsUpdateRequest request)
    {
        if (!this.ModelState.IsValid)
        {
            return this.BadRequest(this.ModelState);
        }

        if (id != request.UserID)
        {
            return this.BadRequest();
        }

        var user = await this.context.Set<User>().FindAsync(id).ConfigureAwait(false);
        if (user == null)
        {
            return this.NotFound();
        }

        user.EmailOne = NormalizeEmail(request.EmailOne);
        user.EmailTwo = string.IsNullOrWhiteSpace(request.EmailTwo) ? null : request.EmailTwo.Trim();
        user.FirstName = string.IsNullOrWhiteSpace(request.FirstName) ? null : request.FirstName.Trim();
        user.FamName = string.IsNullOrWhiteSpace(request.FamName) ? null : request.FamName.Trim();
        user.Username = NormalizeUsername(request.Username);
        user.ArtistInGoodStanding = request.ArtistInGoodStanding;
        user.BannedDate = request.BannedDate;
        user.BannedReason = string.IsNullOrWhiteSpace(request.BannedReason) ? null : request.BannedReason.Trim();
        user.BirthDate = request.BirthDate;
        user.BoardValidated = request.BoardValidated;
        user.CompanyName = string.IsNullOrWhiteSpace(request.CompanyName) ? null : request.CompanyName.Trim();
        user.CompanyTitle = string.IsNullOrWhiteSpace(request.CompanyTitle) ? null : request.CompanyTitle.Trim();
        user.Joined = request.Joined;
        user.MembershipBanned = request.MembershipBanned;
        user.Moderator = request.Moderator;
        user.Nationality = string.IsNullOrWhiteSpace(request.Nationality) ? null : request.Nationality.Trim();
        user.PreferredName = string.IsNullOrWhiteSpace(request.PreferredName) ? null : request.PreferredName.Trim();
        user.HideFromPublic = request.HideFromPublic;
        user.IsPublished = request.IsPublished;
        user.IsModerationBlocked = request.IsModerationBlocked;
        user.DeathDate = request.DeathDate;
        user.Gender = string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim();
        user.GalleryID = request.GalleryID;
        user.CoverPicID = request.CoverPicID;
        user.ProfilePicID = request.ProfilePicID;

        try
        {
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            await this.TryWriteAuditLogAsync(
                shortText: "User updated",
                tags: "scope=audit;entity=user;event=profile;operation=update;result=success;channel=db",
                userId: user.UserID,
                loggedData: $"userId={user.UserID};username={user.Username};isPublished={user.IsPublished};isModerationBlocked={user.IsModerationBlocked}")
                .ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!this.UserExists(id))
            {
                return this.NotFound();
            }
            else
            {
                throw;
            }
        }

        return this.NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await this.context.Set<User>().FindAsync(id).ConfigureAwait(false);
        if (user == null)
        {
            return this.NotFound();
        }

        this.context.Set<User>().Remove(user);
        await this.context.SaveChangesAsync().ConfigureAwait(false);

        await this.TryWriteAuditLogAsync(
            shortText: "User deleted",
            tags: "scope=audit;entity=user;event=profile;operation=delete;result=success;channel=db",
            critical: true,
            userId: id,
            loggedData: $"userId={id};username={user.Username}")
            .ConfigureAwait(false);

        return this.NoContent();
    }

    private async Task TryWriteAuditLogAsync(
        string shortText,
        string tags,
        bool critical = false,
        string? longText = null,
        string? loggedData = null,
        int? userId = null,
        int? artistId = null,
        int? listingId = null)
    {
        try
        {
            this.context.Set<Log>().Add(new Log
            {
                ShortText = shortText,
                Tags = tags,
                Critical = critical,
                LongText = longText,
                LoggedData = loggedData,
                UserID = userId,
                ArtistID = artistId,
                ListingID = listingId,
                LogTimestamp = DateTime.UtcNow,
            });

            await this.context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to write user audit log. Tags: {Tags}", tags);
        }
    }

    private bool UserExists(int id)
    {
        return this.context.Set<User>().Any(e => e.UserID == id);
    }

    private static UserPublicSummaryResponse BuildPublicSummary(User user)
    {
        return new UserPublicSummaryResponse
        {
            UserID = user.UserID,
            Username = user.Username ?? string.Empty,
            PreferredName = string.IsNullOrWhiteSpace(user.PreferredName) ? user.Username ?? string.Empty : user.PreferredName,
            ProfilePic = user.ProfilePic?.URL,
        };
    }

    private static UserPrivateDetailsResponse BuildPrivateDetails(User user)
    {
        return new UserPrivateDetailsResponse
        {
            UserID = user.UserID,
            EmailOne = user.EmailOne,
            EmailTwo = user.EmailTwo,
            FirstName = user.FirstName,
            FamName = user.FamName,
            Username = user.Username,
            ArtistInGoodStanding = user.ArtistInGoodStanding,
            BannedDate = user.BannedDate,
            BannedReason = user.BannedReason,
            BirthDate = user.BirthDate,
            BoardValidated = user.BoardValidated,
            CompanyName = user.CompanyName,
            CompanyTitle = user.CompanyTitle,
            Joined = user.Joined,
            MembershipBanned = user.MembershipBanned,
            Moderator = user.Moderator,
            Nationality = user.Nationality,
            PreferredName = user.PreferredName,
            HideFromPublic = user.HideFromPublic,
            IsPublished = user.IsPublished,
            IsModerationBlocked = user.IsModerationBlocked,
            DeathDate = user.DeathDate,
            Gender = user.Gender,
            GalleryID = user.GalleryID,
            CoverPicID = user.CoverPicID,
            ProfilePicID = user.ProfilePicID,
        };
    }

    private static string NormalizeUsername(string username)
    {
        return string.IsNullOrWhiteSpace(username) ? string.Empty : username.Trim().ToLowerInvariant();
    }

    private static string NormalizeEmail(string email)
    {
        return string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();
    }

    private IQueryable<User> PublicUsersQuery()
    {
        return this.context.Users
            .AsNoTracking()
            .Include(u => u.UserPrivacy)
            .Include(u => u.ProfilePic)
            .Where(u =>
                u.IsPublished &&
                !u.IsModerationBlocked &&
                !u.HideFromPublic &&
                (u.UserPrivacy == null || !u.UserPrivacy.HideProfileFromPublic));
    }

    private async Task<bool> IsUsernameUniqueAsync(string username, int? excludeUserId = null)
    {
        var query = this.context.Set<User>().AsQueryable();

        if (excludeUserId.HasValue)
        {
            query = query.Where(u => u.UserID != excludeUserId.Value);
        }

        return !await query.AnyAsync(u => u.Username != null && u.Username.ToLower() == username).ConfigureAwait(false);
    }

    private async Task CreateDefaultPreferenceRowsAsync(int userId)
    {
        var userSettings = new UserSettings { UserID = userId };
        var userPrivacy = new UserPrivacy { UserID = userId, HideProfileFromPublic = false, HidingFrom_NameAndDescription = "", HidingFromAbuser = false };
        var userPreference = new UserPreference { UserID = userId, MetricOrImperial = "Metric", ThemePreference = "Light" };

        this.context.Set<UserSettings>().Add(userSettings);
        this.context.Set<UserPrivacy>().Add(userPrivacy);
        this.context.Set<UserPreference>().Add(userPreference);

        await this.context.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task<bool> IsModeratorAsync(int userId)
    {
        if (userId <= 0)
        {
            return false;
        }

        return await this.context.Users
            .Where(u => u.UserID == userId)
            .Select(u => u.Moderator)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
    }

    private async Task<bool> IsActiveStaffAsync(int userId)
    {
        if (userId <= 0)
        {
            return false;
        }

        var now = DateTime.UtcNow;

        return await this.context.Staffs
            .AnyAsync(staff => staff.UserID == userId && staff.Active && (!staff.LeaveDate.HasValue || staff.LeaveDate.Value > now))
            .ConfigureAwait(false);
    }

    private async Task<bool> HasPrivilegedDetailsAccessAsync(int targetUserId, int viewerUserId)
    {
        if (viewerUserId <= 0)
        {
            return false;
        }

        if (viewerUserId == targetUserId)
        {
            return true;
        }

        if (await this.IsModeratorAsync(viewerUserId).ConfigureAwait(false))
        {
            return true;
        }

        return await this.IsActiveStaffAsync(viewerUserId).ConfigureAwait(false);
    }
}

public class UserPublicSummaryResponse
{
    public int UserID { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PreferredName { get; set; } = string.Empty;

    public string? ProfilePic { get; set; }
}

public class UserPrivateDetailsResponse
{
    public int UserID { get; set; }

    public string EmailOne { get; set; } = string.Empty;

    public string? EmailTwo { get; set; }

    public string? FirstName { get; set; }

    public string? FamName { get; set; }

    public string? Username { get; set; }

    public bool ArtistInGoodStanding { get; set; }

    public DateTime? BannedDate { get; set; }

    public string? BannedReason { get; set; }

    public DateTime? BirthDate { get; set; }

    public bool BoardValidated { get; set; }

    public string? CompanyName { get; set; }

    public string? CompanyTitle { get; set; }

    public DateTime Joined { get; set; }

    public bool MembershipBanned { get; set; }

    public bool Moderator { get; set; }

    public string? Nationality { get; set; }

    public string? PreferredName { get; set; }

    public bool HideFromPublic { get; set; }

    public bool IsPublished { get; set; }

    public bool IsModerationBlocked { get; set; }

    public DateTime? DeathDate { get; set; }

    public string? Gender { get; set; }

    public int? GalleryID { get; set; }

    public int? CoverPicID { get; set; }

    public int? ProfilePicID { get; set; }
}

public class UserUsernameReservationRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;

    public string? Title { get; set; }

    [Required]
    public string Email { get; set; } = string.Empty;
}

public class UserUsernameUpdateRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;

    public string? Title { get; set; }
}

public class UserUsernameReservationResponse
{
    public int UserID { get; set; }

    public string Username { get; set; } = string.Empty;

    public string? Title { get; set; }

    public string Email { get; set; } = string.Empty;
}

public class UserDetailsUpdateRequest
{
    public int UserID { get; set; }

    [Required]
    public string EmailOne { get; set; } = string.Empty;

    public string? EmailTwo { get; set; }

    public string? FirstName { get; set; }

    public string? FamName { get; set; }

    [Required]
    public string Username { get; set; } = string.Empty;

    public bool ArtistInGoodStanding { get; set; }

    public DateTime? BannedDate { get; set; }

    public string? BannedReason { get; set; }

    public DateTime? BirthDate { get; set; }

    public bool BoardValidated { get; set; }

    public string? CompanyName { get; set; }

    public string? CompanyTitle { get; set; }

    public DateTime Joined { get; set; }

    public bool MembershipBanned { get; set; }

    public bool Moderator { get; set; }

    public string? Nationality { get; set; }

    public string? PreferredName { get; set; }

    public bool HideFromPublic { get; set; }

    public bool IsPublished { get; set; }

    public bool IsModerationBlocked { get; set; }

    public DateTime? DeathDate { get; set; }

    public string? Gender { get; set; }

    public int? GalleryID { get; set; }

    public int? CoverPicID { get; set; }

    public int? ProfilePicID { get; set; }
}
