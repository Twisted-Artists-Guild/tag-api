// <copyright file="Linker_Vendor_To_User.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;
public class Linker_VendorToUser
{
    public bool EmergencyManager { get; set; }

    public int MemberID { get; set; }

    public string Note { get; set; }

    public string Role { get; set; }

    public string Title { get; set; }

    public int VendorID { get; set; }

    [Key]
    public int Linker_VendorToUserID { get; set; }

    public Artist User { get; set; }
    public Vendor Vendor { get; set; }
}
