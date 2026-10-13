// <copyright file="UserPrivacy.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace TAGWEBAPI.Models;
public class UserPrivacy
{
    [Key]
    public int UserPrivacyID { get; set; }

    public bool HideProfileFromPublic { get; set; }

    public string HidingFrom_NameAndDescription { get; set; }

    public bool HidingFromAbuser { get; set; }

    public int UserID { get; set; }
    public User User { get; set; }
}
