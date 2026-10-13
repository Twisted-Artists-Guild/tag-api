// <copyright file="Linker_User_Favorite.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;
public class Linker_UserFavorite
{
    public int ArtistID { get; set; }

    [Key]
    public int Linker_UserFavoriteID { get; set; }

    public DateTime FavoriteDate { get; set; }

    public int ListingID { get; set; }

    public string Note { get; set; }

    public int Order { get; set; }

    public int UserID { get; set; }

    public Artist Artist { get; set; }

    public Listing Listing { get; set; }

    public User User { get; set; }
}
