// <copyright file="CreditParty.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace TAGWEBAPI.Models;

public class CreditParty
{
    [Key]
    public int CreditPartyID { get; set; }

    public int? UserID { get; set; }

    public int? ArtistID { get; set; }

    public string? DisplayName { get; set; }

    public string? ExternalURL { get; set; }

    public string? BioNote { get; set; }

    public DateTime Created { get; set; }
}