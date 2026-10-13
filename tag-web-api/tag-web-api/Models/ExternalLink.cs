// <copyright file="ExternalLink.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

/// <summary>
/// Represents an external link to be associated with artists or users.
/// </summary>
public class ExternalLink
{
    [Key]
    public int ExternalLinkID { get; set; }

    [StringLength(1000)]
    public string? URL { get; set; }

    [StringLength(255)]
    public string? Description { get; set; }

    [StringLength(100)]
    public string? Handle { get; set; }

    public int? ContactLabelID { get; set; }

    public int? LinkCategoryID { get; set; }

    [ForeignKey("LinkCategoryID")]
    public virtual LinkCategory? LinkCategory { get; set; }

    [ForeignKey("ContactLabelID")]
    public virtual ContactLabel? ContactLabel { get; set; }
}
