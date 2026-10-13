// <copyright file="CreditRole.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace TAGWEBAPI.Models;

public class CreditRole
{
    [Key]
    public int CreditRoleID { get; set; }

    public string KeyName { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }
}