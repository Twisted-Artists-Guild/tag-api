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
}
