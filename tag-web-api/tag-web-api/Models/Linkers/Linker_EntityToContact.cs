// <copyright file="Linker_EntityToContact.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

public enum ContactScope
{
    Private = 0,
    Primary = 1,
    Secondary = 2
}

public static class LinkedEntityTypes
{
    public const string User = "User";
    public const string Artist = "Artist";
    public const string Venue = "Venue";
    public const string Vendor = "Vendor";

    public static readonly string[] All = [User, Artist, Venue, Vendor];
}

public class Linker_EntityToContact
{
    [Key]
    public int Linker_EntityToContactID { get; set; }

    public int ContactID { get; set; }

    [Required]
    [StringLength(50)]
    public string EntityType { get; set; } = string.Empty;

    public int EntityID { get; set; }

    public ContactScope Scope { get; set; } = ContactScope.Secondary;

    public int DisplayOrder { get; set; } = 0;

    [ForeignKey("ContactID")]
    public Contact Contact { get; set; } = null!;
}