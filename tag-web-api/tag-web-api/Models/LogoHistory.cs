// <copyright file="LogoHistory.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

public class LogoHistory
{
    [Key]
    public int LogoHistoryID { get; set; }

    [Required]
    [MaxLength(50)]
    public string EntityType { get; set; } = string.Empty;

    public int EntityID { get; set; }

    [ForeignKey(nameof(Picture))]
    public int PictureID { get; set; }

    public virtual Picture Picture { get; set; } = null!;

    public bool IsActive { get; set; }

    public bool IsArchived { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ArchivedUtc { get; set; }

    public DateTime? RestoredUtc { get; set; }

    public int? ReplacedByHistoryID { get; set; }

    public int? PreviousLogoHistoryID { get; set; }
}
