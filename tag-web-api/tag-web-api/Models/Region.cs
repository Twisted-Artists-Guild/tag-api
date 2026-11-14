// <copyright file="Region.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models;

using System.ComponentModel.DataAnnotations;
using System.Globalization;

/// <summary>
/// A staff-managed regional community grouping shared by venues and artists. A region is statewide, covers several
/// states, or is an area within one state (such as a metro area). Regions are never deleted: a deactivated region stays
/// on the venues and artists that already use it but can no longer be chosen for new ones.
/// </summary>
public class Region
{
    public const int NameMaxLength = 100;

    public int RegionID { get; set; }

    [Required]
    [MaxLength(NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    // Stable URL-safe key used by filters. It is set when the region is created and does not change on rename.
    [Required]
    [MaxLength(NameMaxLength)]
    public string Slug { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Type { get; set; } = RegionTypes.Statewide;

    // Two-letter state codes the region covers, for example ["GA"] or ["GA", "AL", "TN"].
    public List<string> States { get; set; } = new();

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Checks the type and the states together and returns them in canonical form (type spelled the standard way,
    /// state codes upper case, no duplicates). A statewide region and a region within a state cover exactly one state;
    /// a multistate region covers two or more.
    /// </summary>
    public static bool TryNormalize(string? rawType, IEnumerable<string?>? rawStates, out string type, out List<string> states, out string? error)
    {
        type = string.Empty;
        states = new List<string>();
        error = null;

        var matchedType = RegionTypes.All.FirstOrDefault(t => string.Equals(t, rawType?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (matchedType == null)
        {
            error = $"Type must be one of: {string.Join(", ", RegionTypes.All)}.";
            return false;
        }

        foreach (var raw in rawStates ?? Enumerable.Empty<string?>())
        {
            var code = (raw ?? string.Empty).Trim().ToUpper(CultureInfo.InvariantCulture);
            if (!UsStates.Names.ContainsKey(code))
            {
                error = $"'{Shorten(code)}' is not a state code. Use two-letter codes such as GA.";
                return false;
            }

            if (!states.Contains(code))
            {
                states.Add(code);
            }
        }

        if (matchedType == RegionTypes.Multistate)
        {
            if (states.Count < 2)
            {
                error = "A multistate region must cover at least two states.";
                return false;
            }
        }
        else if (states.Count != 1)
        {
            error = matchedType == RegionTypes.Statewide
                ? "A statewide region must cover exactly one state."
                : "A region within a state must name exactly one state.";
            return false;
        }

        type = matchedType;
        states.Sort(StringComparer.Ordinal);
        return true;
    }

    private static string Shorten(string value) => value.Length <= 10 ? value : value[..10] + "…";
}

public static class RegionTypes
{
    public const string Statewide = "Statewide";
    public const string Multistate = "Multistate";
    public const string WithinState = "WithinState";

    public static readonly IReadOnlyList<string> All = new[] { Statewide, Multistate, WithinState };
}

/// <summary>The 50 states and the District of Columbia.</summary>
public static class UsStates
{
    public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["AL"] = "Alabama", ["AK"] = "Alaska", ["AZ"] = "Arizona", ["AR"] = "Arkansas", ["CA"] = "California",
        ["CO"] = "Colorado", ["CT"] = "Connecticut", ["DE"] = "Delaware", ["DC"] = "District of Columbia", ["FL"] = "Florida",
        ["GA"] = "Georgia", ["HI"] = "Hawaii", ["ID"] = "Idaho", ["IL"] = "Illinois", ["IN"] = "Indiana",
        ["IA"] = "Iowa", ["KS"] = "Kansas", ["KY"] = "Kentucky", ["LA"] = "Louisiana", ["ME"] = "Maine",
        ["MD"] = "Maryland", ["MA"] = "Massachusetts", ["MI"] = "Michigan", ["MN"] = "Minnesota", ["MS"] = "Mississippi",
        ["MO"] = "Missouri", ["MT"] = "Montana", ["NE"] = "Nebraska", ["NV"] = "Nevada", ["NH"] = "New Hampshire",
        ["NJ"] = "New Jersey", ["NM"] = "New Mexico", ["NY"] = "New York", ["NC"] = "North Carolina", ["ND"] = "North Dakota",
        ["OH"] = "Ohio", ["OK"] = "Oklahoma", ["OR"] = "Oregon", ["PA"] = "Pennsylvania", ["RI"] = "Rhode Island",
        ["SC"] = "South Carolina", ["SD"] = "South Dakota", ["TN"] = "Tennessee", ["TX"] = "Texas", ["UT"] = "Utah",
        ["VT"] = "Vermont", ["VA"] = "Virginia", ["WA"] = "Washington", ["WV"] = "West Virginia", ["WI"] = "Wisconsin",
        ["WY"] = "Wyoming",
    };
}
