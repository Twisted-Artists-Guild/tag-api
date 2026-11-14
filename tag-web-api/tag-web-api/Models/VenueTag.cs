// <copyright file="VenueTag.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models;

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// A free-form amenity tag such as "parking" or "wheelchair accessible". Names are stored normalized
/// (trimmed, lowercase, single spaces), so the same tag is never stored twice.
/// </summary>
public class VenueTag
{
    public const int NameMaxLength = 30;
    public const int MaxTagsPerVenue = 15;

    // Letters and numbers of any language, plus space, hyphen, apostrophe and ampersand. No markup characters can pass.
    private static readonly Regex AllowedNameRegex = new(@"^[\p{L}\p{N}][\p{L}\p{N} '&-]*$", RegexOptions.Compiled);

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public int VenueTagID { get; set; }

    [Required]
    [MaxLength(NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Trims, lowercases and collapses spaces. Returns false with a message that is safe to show when the tag is
    /// empty, too long or has characters that are not allowed.
    /// </summary>
    public static bool TryNormalize(string? raw, out string name, out string? error)
    {
        name = string.Empty;
        error = null;

        var normalized = WhitespaceRegex
            .Replace((raw ?? string.Empty).Normalize(NormalizationForm.FormC).Trim(), " ")
            .ToLower(CultureInfo.InvariantCulture);

        if (normalized.Length == 0)
        {
            error = "A tag can't be empty.";
            return false;
        }

        if (normalized.Length > NameMaxLength)
        {
            error = $"The tag '{Shorten(normalized)}' is too long. Tags can be at most {NameMaxLength} characters.";
            return false;
        }

        if (!AllowedNameRegex.IsMatch(normalized))
        {
            error = $"The tag '{Shorten(normalized)}' has characters that aren't allowed. Use letters, numbers, spaces, hyphens, apostrophes and &, and start with a letter or number.";
            return false;
        }

        name = normalized;
        return true;
    }

    private static string Shorten(string value) => value.Length <= 40 ? value : value[..40] + "…";
}
