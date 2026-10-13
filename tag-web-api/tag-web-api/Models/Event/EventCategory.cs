// <copyright file="EventCategory.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace TAGWEBAPI.Models;

public class EventCategory
{
    [Key]
    public int EventCategoryID { get; set; }

    [Required]
    public string Category { get; set; }

    [Required]
    public string CategoryKey { get; set; }

    [Required]
    public string Description { get; set; }

    [Required]
    public string Tags { get; set; }
}
