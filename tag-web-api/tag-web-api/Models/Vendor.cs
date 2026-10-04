// <copyright file="Vendor.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace TAGWEBAPI.Models;
public class Vendor
{
    [Key]
    public int VendorID { get; set; }

    public string CompanyName { get; set; }

    public string? CompanyName_Plaintext { get; set; }

    public DateTime ContractExpires { get; set; }

    public string LinkToContract { get; set; }

    public string LinkToMSA { get; set; }

    public DateTime MSA_Executed { get; set; }

    public string NotesOnContracts { get; set; }

    public string? NotesOnContracts_Plaintext { get; set; }

    public string NotesOnVendors { get; set; }

    public string? NotesOnVendors_Plaintext { get; set; }

    public string POCEmail { get; set; }

    public string POCName { get; set; }

    public string POCPhone { get; set; }

    public bool IsPublished { get; set; }

    public bool IsModerationBlocked { get; set; }

    public int? LogoPicID { get; set; }

    public virtual Picture? LogoPic { get; set; }
}
