// <copyright file="Forms_Metadata.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace TAGWEBAPI.Models;
public class Forms_Metadata
{
    [Key]
    public int Forms_MetadataID { get; set; }

    public string? APIURLpostfix { get; set; }

    public string? FormBody { get; set; }

    public string? FormStyle { get; set; }

    public string? H1 { get; set; }

    public string? H2 { get; set; }

    public string? H3 { get; set; }

    public string Name { get; set; }

    public string? RequestType { get; set; }

    public string? SegmentationType { get; set; }

    public ICollection<Forms_Field> Forms_Fields { get; set; }
}
