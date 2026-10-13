// <copyright file="EventStatus.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models;

public enum EventStatus
{
    Draft = 0,
    PendingReview = 1,
    Published = 2,
    Archived = 3,
    Cancelled = 4,
    ModerationBlocked = 5,
}