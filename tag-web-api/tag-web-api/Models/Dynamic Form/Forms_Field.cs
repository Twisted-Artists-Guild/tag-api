// <copyright file="Forms_Field.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace TAGWEBAPI.Models;
public class Forms_Field
{
    [Key]
    public int Forms_FieldID { get; set; }

    public string Name { get; set; }

    public bool? Autocomplete { get; set; }

    public bool? Autofocus { get; set; }

    public string? ClassName { get; set; }

    public string? DefaultValue { get; set; }

    public bool? Disabled { get; set; }

    public int? FieldOrder { get; set; }

    public string? Height { get; set; }

    public bool? Hidden { get; set; }

    public string? Label { get; set; }

    public int? Max { get; set; }

    public int? Maxlength { get; set; }

    public int? Min { get; set; }

    public int? Minlength { get; set; }

    public string? Placeholder { get; set; }

    public bool? IsReadOnly { get; set; }

    public bool? IsRequired { get; set; }

    public string? Type { get; set; }

    public string? RegexValidationPattern { get; set; }

    public string? Width { get; set; }

    public int Forms_MetadataID { get; set; }

    public string? Options { get; set; }

    [JsonIgnore]
    public Forms_Metadata Forms_Metadata { get; set; }
}
