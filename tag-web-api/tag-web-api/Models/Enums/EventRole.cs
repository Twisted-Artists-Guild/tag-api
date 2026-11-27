// <copyright file="EventRole.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models;

/// <summary>
/// What someone may do with an event. Each role includes everything the roles below it can do.
/// Owner is never stored: it is the artist or venue the event belongs to (and platform staff).
/// </summary>
public enum EventRole
{
    None = 0,
    ReadOnly = 1,
    CheckIn = 2,
    Staff = 3,
    Admin = 4,
    Owner = 5,
}
