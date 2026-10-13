// <copyright file="Blog.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

public class Blog
{
    [Key]
    public int BlogID { get; set; }

    [Required]
    public string Body { get; set; }

    public string? Body_Plaintext { get; set; }

    [Required]
    public string Byline { get; set; }

    public string? Byline_Plaintext { get; set; }

    public DateTime Created { get; set; }

    public DateTime? Modified { get; set; }

    [Required]
    public string Path { get; set; }

    [Required]
    public string Title { get; set; }

    public string? Title_Plaintext { get; set; }

    [ForeignKey("User")]
    public int UserID { get; set; }

    public int? GalleryID { get; set; }

    public int? CoverPicID { get; set; }

    public BlogStatus StatusID { get; set; } = BlogStatus.Draft;

    [ValidateNever]
    public virtual User User { get; set; }

    [ValidateNever]
    public virtual Gallery? Gallery { get; set; }

    [ValidateNever]
    public virtual Picture? CoverPic { get; set; }
}
