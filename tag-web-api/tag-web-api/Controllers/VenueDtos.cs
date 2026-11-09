// <copyright file="VenueDtos.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Controllers
{
    /// <summary>
    /// The venue fields a client may write. Anything else in a request body (logo, moderation flags, ids of
    /// related records) is ignored, so a client cannot overwrite data it does not own.
    /// </summary>
    public class VenueWriteRequest
    {
        public string? Name { get; set; }

        public int AddressID { get; set; }

        public int? ExternalLinkID { get; set; }

        public int? PhoneContactID { get; set; }

        public string? Description { get; set; }

        public string? EventSourceUrl { get; set; }
    }

    public class VenueCreateRequest : VenueWriteRequest
    {
    }

    public class VenueUpdateRequest : VenueWriteRequest
    {
        public int VenueID { get; set; }

        // Null means "leave unchanged", so a form that does not send the flag cannot unpublish a venue by accident.
        public bool? IsPublished { get; set; }

        // Only staff may change this; a value that differs from the stored one is rejected for everyone else.
        public bool? IsModerationBlocked { get; set; }
    }

    public class VenueOwnerAssignRequest
    {
        public int UserID { get; set; }
    }

    /// <summary>A user who manages a venue. Only the fields staff need to tell users apart are exposed.</summary>
    public class VenueOwnerDto
    {
        public int UserID { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public string? Username { get; set; }

        public string? Email { get; set; }

        public string Role { get; set; } = string.Empty;
    }

    public class VenueAdminRowDto
    {
        public int VenueID { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public bool IsPublished { get; set; }

        public bool IsModerationBlocked { get; set; }

        public List<VenueOwnerDto> Owners { get; set; } = new();
    }

    /// <summary>One page of venues for the staff screen; NextAfterId is the cursor for the next page, or null at the end.</summary>
    public class VenueAdminPageDto
    {
        public List<VenueAdminRowDto> Items { get; set; } = new();

        public int? NextAfterId { get; set; }
    }

    public class UserLookupDto
    {
        public int UserID { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public string? Username { get; set; }

        public string? Email { get; set; }
    }
}
