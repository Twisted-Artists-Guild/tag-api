// <copyright file="VenueSlugDtos.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Controllers
{
    using System.ComponentModel.DataAnnotations;

    public class VenueSlugReservationRequest
    {
        [Required]
        public string Slug { get; set; } = string.Empty;

        public string? Title { get; set; }
    }

    public class VenueSlugUpdateRequest
    {
        [Required]
        public string Slug { get; set; } = string.Empty;

        public string? Title { get; set; }
    }

    public class VenueSlugReservationResponse
    {
        public int VenueID { get; set; }

        public string Slug { get; set; } = string.Empty;

        public string? Title { get; set; }
    }
}
