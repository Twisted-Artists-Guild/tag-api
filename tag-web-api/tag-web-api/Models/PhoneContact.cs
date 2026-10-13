// <copyright file="PhoneContact.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

public class PhoneContact
{
    [Key]
    public int PhoneContactID { get; set; }

    public int? PhoneContactLabelID { get; set; }

    public int? ContactLabelID { get; set; }

    [StringLength(50)]
    public string? PhoneNumber { get; set; }

    [StringLength(255)]
    public string? Description { get; set; }

    public PhoneContactLabel? PhoneContactLabel { get; set; }

    public ContactLabel? ContactLabel { get; set; }
}
