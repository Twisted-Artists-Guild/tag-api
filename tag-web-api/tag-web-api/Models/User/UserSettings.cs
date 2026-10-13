// <copyright file="UserSettings.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace TAGWEBAPI.Models;
public class UserSettings
{
    [Key]
    public int UserSettingsID { get; set; }

    public int UserID { get; set; }
    public User User { get; set; }
}
