// <copyright file="UnifiedWorkflow.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models;

/// <summary>
/// Unified workflow tracking for artists, listings, users, vendors, and venues.
/// Tracks progression through workflow steps for each entity type.
/// </summary>
public class UnifiedWorkflow
{
    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    public int UnifiedWorkflowID { get; set; }

    /// <summary>
    /// Gets or sets the entity identifier (ArtistID, ListingID, UserID, VendorID, or VenueID).
    /// </summary>
    public int EntityID { get; set; }

    /// <summary>
    /// Gets or sets the entity type (Artist, Listing, User, Vendor, or Venue).
    /// </summary>
    public string EntityType { get; set; } = null!;

    /// <summary>
    /// Gets or sets the workflow name (e.g., "ArtistCreation", "ArtistProfileUpdate").
    /// </summary>
    public string WorkflowName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the current step key within the workflow.
    /// </summary>
    public string StepKey { get; set; } = null!;

    /// <summary>
    /// Gets or sets a value indicating whether this step has been completed.
    /// </summary>
    public bool IsCompleted { get; set; } = false;

    /// <summary>
    /// Gets or sets the timestamp when this step was completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last update.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who last updated this workflow step.
    /// </summary>
    public int? UpdatedByUserID { get; set; }
}
