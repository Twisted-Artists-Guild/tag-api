// <copyright file="VenueCategory.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// A staff-managed kind of venue (for example "Theaters"). Categories are never deleted: a deactivated category stays
/// on the venues that already use it but can no longer be chosen for new ones.
/// </summary>
public class VenueCategory
{
    public const int NameMaxLength = 100;

    public int VenueCategoryID { get; set; }

    [Required]
    [MaxLength(NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    // Stable URL-safe key used by filters. It is set when the category is created and does not change on rename,
    // so a shared filter link keeps working.
    [Required]
    [MaxLength(NameMaxLength)]
    public string Slug { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
