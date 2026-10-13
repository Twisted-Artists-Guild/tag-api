// <copyright file="UserDetails.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace TAGWEBAPI.Models;

public class User
{
    [Key]
    public int UserID { get; set; }

    [Required]
    public string EmailOne { get; set; }

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

    public virtual Gallery? Gallery { get; set; }

    public virtual Picture? CoverPic { get; set; }

    public virtual Picture? ProfilePic { get; set; }

    // Navigation property
    public ICollection<Staff> Staffs { get; set; }

    public UserSettings UserSettings { get; set; }
    public UserPrivacy UserPrivacy { get; set; }
    public UserPreference UserPreference { get; set; }

    /// <summary>
    /// Gets or sets the collection of Transactions associated with this user
    /// </summary>
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    public ICollection<BugReport> BugReports { get; set; } = new List<BugReport>();
}
