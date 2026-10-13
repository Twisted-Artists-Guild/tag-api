// <copyright file="MediaCredit.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace TAGWEBAPI.Models;

public class MediaCredit
{
    [Key]
    public int MediaCreditID { get; set; }

    public int? PictureID { get; set; }

    public int? VideoID { get; set; }

    public int? BlogID { get; set; }

    public int CreditPartyID { get; set; }

    public int CreditRoleID { get; set; }

    public string? CreditText { get; set; }

    public int SortOrder { get; set; }
}