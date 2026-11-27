using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Controllers;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;
using Xunit;
using static tagApiUnitTests.VenueTestSupport;

namespace tagApiUnitTests;

// Story #420: event model v2. Time zone, ticket sales window and check-in window on an event.
public class EventModelTests
{
    private const int OwnerId = 10;
    private const int VenueId = 1;

    private static async Task<DbContextOptions<TAGDBContext>> SeedAsync(string name)
    {
        var options = CreateOptions($"eventmodel-{name}");
        await VenueTestSupport.SeedAsync(options, new Venue { VenueID = VenueId, Name = "Hall", AddressID = AddressId, IsPublished = true }, OwnerId);
        await using var seed = new TAGDBContext(options);
        seed.Users.Add(new User { UserID = OwnerId, EmailOne = "o@example.com", Username = "owner" });
        await seed.SaveChangesAsync();
        return options;
    }

    private static EventController Controller(TAGDBContext context) => new(context)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, OwnerId.ToString()) }, "test")),
            },
        },
    };

    private static EventDetailsRequest Request(Action<EventDetailsRequest>? change = null)
    {
        var start = DateTime.UtcNow.AddDays(10);
        var request = new EventDetailsRequest
        {
            Title = "Class",
            Description = "<p>Learn.</p>",
            PointOfContact = "a@example.com",
            StartTime = start,
            EndTime = start.AddHours(2),
        };
        change?.Invoke(request);
        return request;
    }

    private static int Status(IActionResult result) => ((IStatusCodeActionResult)result).StatusCode ?? 200;

    [Fact]
    public async Task NewEvent_StoresTimeZoneSalesWindowAndCheckInWindow()
    {
        var options = await SeedAsync(nameof(NewEvent_StoresTimeZoneSalesWindowAndCheckInWindow));
        await using var context = new TAGDBContext(options);
        var salesStart = DateTime.UtcNow.AddDays(1);

        var result = await Controller(context).CreateVenueEvent(VenueId, Request(r =>
        {
            r.TimeZoneId = " UTC ";
            r.SalesStart = salesStart;
            r.SalesEnd = salesStart.AddDays(5);
            r.CheckInWindowMinutes = 120;
        }));

        Assert.Equal(201, Status(result));
        await using var check = new TAGDBContext(options);
        var saved = await check.Events.AsNoTracking().SingleAsync();
        Assert.Equal("UTC", saved.TimeZoneId);
        Assert.NotNull(saved.SalesEnd);
        Assert.Equal(120, saved.CheckInWindowMinutes);
    }

    [Fact]
    public async Task NewEvent_DefaultsCheckInToOneHourAndNoSalesLimit()
    {
        var options = await SeedAsync(nameof(NewEvent_DefaultsCheckInToOneHourAndNoSalesLimit));
        await using var context = new TAGDBContext(options);

        Assert.Equal(201, Status(await Controller(context).CreateVenueEvent(VenueId, Request())));

        await using var check = new TAGDBContext(options);
        var saved = await check.Events.AsNoTracking().SingleAsync();
        Assert.Null(saved.TimeZoneId);
        Assert.Null(saved.SalesStart);
        Assert.Equal(Event.DefaultCheckInWindowMinutes, saved.CheckInWindowMinutes);
    }

    [Theory]
    [InlineData("Not/AZone")]
    [InlineData("")]
    public async Task UnknownTimeZone_IsRejected(string zone)
    {
        var options = await SeedAsync($"{nameof(UnknownTimeZone_IsRejected)}-{zone.Length}");
        await using var context = new TAGDBContext(options);

        var result = await Controller(context).CreateVenueEvent(VenueId, Request(r => r.TimeZoneId = zone));

        // A blank time zone just clears it; only an unknown id is an error.
        Assert.Equal(zone.Length == 0 ? 201 : 400, Status(result));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(361)]
    public async Task CheckInWindow_OutsideZeroToSixHours_IsRejected(int minutes)
    {
        var options = await SeedAsync($"{nameof(CheckInWindow_OutsideZeroToSixHours_IsRejected)}-{minutes}");
        await using var context = new TAGDBContext(options);

        var result = await Controller(context).CreateVenueEvent(VenueId, Request(r => r.CheckInWindowMinutes = minutes));

        Assert.Equal(400, Status(result));
        Assert.Empty(await context.Events.ToListAsync());
    }

    [Fact]
    public async Task SalesWindow_MustEndAfterItStartsAndBeforeTheEventEnds()
    {
        var options = await SeedAsync(nameof(SalesWindow_MustEndAfterItStartsAndBeforeTheEventEnds));
        await using var context = new TAGDBContext(options);
        var controller = Controller(context);
        var now = DateTime.UtcNow;

        var backwards = await controller.CreateVenueEvent(VenueId, Request(r => { r.SalesStart = now.AddDays(3); r.SalesEnd = now.AddDays(2); }));
        var afterEvent = await controller.CreateVenueEvent(VenueId, Request(r => r.SalesEnd = now.AddDays(30)));

        Assert.Equal(400, Status(backwards));
        Assert.Equal(400, Status(afterEvent));
    }

    [Fact]
    public async Task UpdatingDetails_ChangesOnlyTheNewFieldsThatWereSent()
    {
        var options = await SeedAsync(nameof(UpdatingDetails_ChangesOnlyTheNewFieldsThatWereSent));
        await using var context = new TAGDBContext(options);
        var controller = Controller(context);
        var created = (CreatedAtActionResult)await controller.CreateVenueEvent(VenueId, Request(r => { r.TimeZoneId = "UTC"; r.CheckInWindowMinutes = 90; }));
        var id = (int)created.RouteValues!["id"]!;

        var result = await controller.UpdateEventDetails(id, new EventDetailsRequest { CheckInWindowMinutes = 30 });

        Assert.Equal(200, Status(result));
        await using var check = new TAGDBContext(options);
        var saved = await check.Events.AsNoTracking().SingleAsync();
        Assert.Equal(30, saved.CheckInWindowMinutes);
        Assert.Equal("UTC", saved.TimeZoneId);
    }
}
