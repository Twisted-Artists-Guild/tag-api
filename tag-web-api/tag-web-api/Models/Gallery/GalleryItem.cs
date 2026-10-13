// <copyright file="GalleryItem.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace TAGWEBAPI.Models;

public class GalleryItem
{
    [Key]
    public int GalleryItemID { get; set; }

    public int GalleryID { get; set; }

    public int? PictureID { get; set; }

    public int? VideoID { get; set; }

    public int SortOrder { get; set; }

    public string? CaptionOverride { get; set; }

    public int? AddedByUserID { get; set; }

    public DateTime Created { get; set; }

    [ValidateNever]
    public virtual Picture? Picture { get; set; }

    [ValidateNever]
    public virtual Video? Video { get; set; }
}