// <copyright file="PhoneContactLabel.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TAGWEBAPI.Models;

public class PhoneContactLabel
{
    [Key]
    public int PhoneContactLabelID { get; set; }

    [Required]
    [StringLength(50)]
    public string Label { get; set; } = string.Empty;

    public ICollection<PhoneContact> PhoneContacts { get; set; } = new List<PhoneContact>();
}
