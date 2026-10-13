// <copyright file="UserToArtist.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models;

using System.ComponentModel.DataAnnotations;

public class Linker_UserToArtist
{
    public int Linker_UserToArtistID { get; set; }

    [Required]
    public int UserID { get; set; }

    [Required]
    public int ArtistID { get; set; }

    [Required]
    public string Role { get; set; }
}
