// <copyright file="VendorSlugDtos.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Controllers
{
    using System.ComponentModel.DataAnnotations;

    public class VendorSlugReservationRequest
    {
        [Required]
        public string Slug { get; set; } = string.Empty;

        public string? Title { get; set; }

        public string? Email { get; set; }
    }

    public class VendorSlugUpdateRequest
    {
        [Required]
        public string Slug { get; set; } = string.Empty;

        public string? Title { get; set; }

        public string? Email { get; set; }
    }

    public class VendorSlugReservationResponse
    {
        public int VendorID { get; set; }

        public string Slug { get; set; } = string.Empty;

        public string? Title { get; set; }

        public string? Email { get; set; }
    }
}
