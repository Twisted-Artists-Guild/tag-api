// <copyright file="BlogStatus.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models;

public enum BlogStatus
{
    Draft = 0,
    PendingReview = 1,
    Published = 2,
    Archived = 3,
    ModerationBlocked = 4,
}