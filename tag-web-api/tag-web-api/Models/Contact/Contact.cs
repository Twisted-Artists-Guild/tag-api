// <copyright file="Contact.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

public class Contact
{
    [Key]
    public int ContactID { get; set; }

    [Required]
    [StringLength(32)]
    public string ContactType { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Label { get; set; }

    public int? ContactLabelID { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    [StringLength(1000)]
    public string? Value { get; set; }

    [StringLength(255)]
    public string? Handle { get; set; }

    [StringLength(255)]
    public string? Description { get; set; }

    public int? AddressID { get; set; }

    public int? PhoneContactID { get; set; }

    [ForeignKey("AddressID")]
    public Address? Address { get; set; }

    [ForeignKey("PhoneContactID")]
    public PhoneContact? PhoneContact { get; set; }

    [ForeignKey("ContactLabelID")]
    public ContactLabel? ContactLabel { get; set; }

    public virtual ICollection<Linker_EntityToContact> EntityLinks { get; set; } = new List<Linker_EntityToContact>();
}