using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TAGWEBAPI.Controllers;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace tagApiUnitTests;

// Shared setup for the venue tests: an in-memory database, a signed-in (or anonymous) caller, and seed data.
internal static class VenueTestSupport
{
    public const int AddressId = 900;

    public static DbContextOptions<TAGDBContext> CreateOptions(string databaseName) =>
        new DbContextOptionsBuilder<TAGDBContext>().UseInMemoryDatabase(databaseName).Options;

    // userId = null builds an anonymous caller.
    public static VenueController CreateController(TAGDBContext context, int? userId = 1)
    {
        var controller = new VenueController(context, NullLogger<VenueController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = BuildUser(userId) } };
        return controller;
    }

    public static Linker_UserToVenueController CreateLinkerController(TAGDBContext context, int? userId = 1)
    {
        var controller = new Linker_UserToVenueController(context);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = BuildUser(userId) } };
        return controller;
    }

    private static ClaimsPrincipal BuildUser(int? userId) =>
        new(userId.HasValue
            ? new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) }, "test")
            : new ClaimsIdentity());

    // Seeds through a separate context so the controller under test starts with a clean change tracker.
    public static async Task SeedAsync(DbContextOptions<TAGDBContext> options, Venue? venue = null, int? ownerUserId = null, params int[] staffUserIds)
    {
        await using var seed = new TAGDBContext(options);
        seed.Set<Address>().Add(new Address { AddressID = AddressId, AddressLine1 = "1 Main St", City = "Atlanta", State = "GA", ZipCode = "30301", Country = "USA" });

        foreach (var staffId in staffUserIds)
        {
            seed.Users.Add(new User { UserID = staffId, EmailOne = $"staff{staffId}@example.com", Username = $"staff{staffId}", Moderator = true });
        }

        if (venue != null)
        {
            seed.Set<Venue>().Add(venue);
            if (ownerUserId.HasValue)
            {
                seed.Set<Linker_UserToVenue>().Add(new Linker_UserToVenue { UserID = ownerUserId.Value, VenueID = venue.VenueID, Role = Linker_UserToVenue.OwnerRole });
            }
        }

        await seed.SaveChangesAsync();
    }

    public static object? Prop(object? value, string name) => value?.GetType().GetProperty(name)?.GetValue(value);
}
