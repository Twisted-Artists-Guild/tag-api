// <copyright file="Gallery.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace TAGWEBAPI.Models;

public class Gallery
{
    [Key]
    public int GalleryID { get; set; }

    public string ScopeType { get; set; } = string.Empty;

    public int ScopeEntityID { get; set; }

    public int? OwnerUserID { get; set; }

    public int? OwnerArtistID { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public bool IsPrimary { get; set; }

    public DateTime Created { get; set; }

    public DateTime Updated { get; set; }

    [ValidateNever]
    public virtual ICollection<GalleryItem> GalleryItems { get; set; } = new List<GalleryItem>();
}