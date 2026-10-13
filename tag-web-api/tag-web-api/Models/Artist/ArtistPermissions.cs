// <copyright file="ArtistPermissions.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

public class ArtistPermissions
{
    [Key]
    public int ArtistPermissionsID { get; set; }

    public bool? OwnerRole { get; set; }

    public bool? POS_Authorized { get; set; }

    [ForeignKey("Artist")]
    public int ArtistID { get; set; }

    public virtual Artist Artist { get; set; }
}
