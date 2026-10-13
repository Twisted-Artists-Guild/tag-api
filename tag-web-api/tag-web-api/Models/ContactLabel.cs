// <copyright file="ContactLabel.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models
{
    using System.ComponentModel.DataAnnotations;

    public class ContactLabel
    {
        [Key]
        public int ContactLabelID { get; set; }

        [Required]
        [StringLength(100)]
        public string Label { get; set; } = string.Empty;
    }
}
