// <copyright file="UserPreference.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace TAGWEBAPI.Models;
public class UserPreference
{
    [Key]
    public int UserPreferenceID { get; set; }

    public string MetricOrImperial { get; set; } = "Metric";

    public string ThemePreference { get; set; } = "tag-theme";

    // JSON-encoded map of profile context keys (e.g. "artist-1") to hex color overrides.
    public string? ContextColorOverrides { get; set; }

    public int UserID { get; set; }

    [ValidateNever]
    public User User { get; set; }
}
