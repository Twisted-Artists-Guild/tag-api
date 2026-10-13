// <copyright file="LinkCategory.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

public class LinkCategory
{
    [Key]
    public int LinkCategoryID { get; set; }

    [Required]
    public string Category { get; set; }

    [Required]
    public string CategoryKey { get; set; }

    public string? Description { get; set; }

    public string? Tags { get; set; }

    [ForeignKey("ParentCategory")]
    public int? ParentLinkCategoryID { get; set; }

    public virtual LinkCategory? ParentCategory { get; set; }

    public virtual ICollection<LinkCategory>? SubCategories { get; set; }
}
