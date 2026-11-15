using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Controllers;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;
using Xunit;
using static tagApiUnitTests.VenueTestSupport;

namespace tagApiUnitTests;

// Story #334: what the public venue page needs. Venue reads say whether anyone manages the venue (never who), and the event
// list can be narrowed to one venue's upcoming events. Database names start with "venuepage-" because they are shared across
// test classes.
public class VenuePublicPageTests
{
    private const int OwnerId = 10;

    private static async Task<DbContextOptions<TAGDBContext>> SeedVenuesAsync(string name, bool published = true, int? ownerUserId = OwnerId)
    {
        var options = CreateOptions($"venuepage-{name}");
        await SeedAsync(options, new Venue { VenueID = 1, Name = "Test Hall", AddressID = AddressId, IsPublished = published }, ownerUserId);
        return options;
    }

    // ---- hasOwner

    [Fact]
    public async Task PublicReads_SayThatAnOwnedVenueHasAnOwner_WithoutSayingWho()
    {
        var options = await SeedVenuesAsync(nameof(PublicReads_SayThatAnOwnedVenueHasAnOwner_WithoutSayingWho));
        await using var context = new TAGDBContext(options);
        var controller = CreateController(context, userId: null);

        var byId = Assert.IsType<OkObjectResult>((await controller.GetVenue(1)).Result);
        var bySlug = Assert.IsType<OkObjectResult>((await controller.GetVenueBySlug("test-hall")).Result);

        Assert.Equal(true, Prop(byId.Value, "HasOwner"));
        Assert.Equal(true, Prop(bySlug.Value, "HasOwner"));
        Assert.Null(Prop(byId.Value, "Owners"));
        Assert.Null(Prop(byId.Value, "OwnerUserID"));
    }

    [Fact]
    public async Task PublicReads_SayThatAnUnownedVenueHasNoOwner()
    {
        var options = await SeedVenuesAsync(nameof(PublicReads_SayThatAnUnownedVenueHasNoOwner), ownerUserId: null);
        await using var context = new TAGDBContext(options);
        var controller = CreateController(context, userId: null);

        var byId = Assert.IsType<OkObjectResult>((await controller.GetVenue(1)).Result);
        var bySlug = Assert.IsType<OkObjectResult>((await controller.GetVenueBySlug("test-hall")).Result);

        Assert.Equal(false, Prop(byId.Value, "HasOwner"));
        Assert.Equal(false, Prop(bySlug.Value, "HasOwner"));
    }

    [Fact]
    public async Task GetVenueById_SaysWhetherTheVenueHasAnOwner_ForAnOwnerLookingAtADraft()
    {
        var options = await SeedVenuesAsync(nameof(GetVenueById_SaysWhetherTheVenueHasAnOwner_ForAnOwnerLookingAtADraft), published: false);
        await using var context = new TAGDBContext(options);

        var ok = Assert.IsType<OkObjectResult>((await CreateController(context, OwnerId).GetVenueById(1)).Result);

        Assert.Equal(true, Prop(ok.Value, "HasOwner"));
    }

    [Fact]
    public async Task ADraftVenue_IsStillNotPublic()
    {
        var options = await SeedVenuesAsync(nameof(ADraftVenue_IsStillNotPublic), published: false);
        await using var context = new TAGDBContext(options);
        var anonymous = CreateController(context, userId: null);

        Assert.IsType<NotFoundResult>((await anonymous.GetVenue(1)).Result);
        Assert.IsType<NotFoundResult>((await anonymous.GetVenueBySlug("test-hall")).Result);
        Assert.IsType<OkObjectResult>((await CreateController(context, OwnerId).GetVenueBySlug("test-hall")).Result);
    }

    // ---- events at a venue

    private static async Task<DbContextOptions<TAGDBContext>> SeedEventsAsync(string name)
    {
        var options = CreateOptions($"venuepage-{name}");
        await VenueTestSupport.SeedAsync(options, new Venue { VenueID = 1, Name = "Test Hall", AddressID = AddressId, IsPublished = true });

        await using var context = new TAGDBContext(options);
        context.Set<Venue>().Add(new Venue { VenueID = 2, Name = "Other Hall", AddressID = AddressId, IsPublished = true });
        context.Artists.Add(new Artist { ArtistID = 5, Title = "Host Artist", Path = "host-artist", Applied = DateTime.UtcNow, Since = DateTime.UtcNow });

        var now = DateTime.UtcNow;
        Event Make(int id, string title, int venueId, DateTime start, DateTime end, EventStatus status = EventStatus.Published) => new()
        {
            EventID = id,
            Title = title,
            Description = "d",
            PointOfContact = "p",
            Path = $"event-{id}",
            ArtistID = 5,
            VenueID = venueId,
            StartTime = start,
            EndTime = end,
            Doors = start,
            StatusID = status,
        };

        context.Events.AddRange(
            Make(1, "Later", 1, now.AddDays(10), now.AddDays(10).AddHours(3)),
            Make(2, "Sooner", 1, now.AddDays(2), now.AddDays(2).AddHours(3)),
            Make(3, "Long gone", 1, now.AddDays(-10), now.AddDays(-10).AddHours(3)),
            Make(4, "Happening now", 1, now.AddHours(-1), now.AddHours(2)),
            Make(5, "Draft", 1, now.AddDays(3), now.AddDays(3).AddHours(3), EventStatus.Draft),
            Make(6, "Blocked", 1, now.AddDays(3), now.AddDays(3).AddHours(3), EventStatus.ModerationBlocked),
            Make(7, "Elsewhere", 2, now.AddDays(4), now.AddDays(4).AddHours(3)));
        await context.SaveChangesAsync();
        return options;
    }

    private static async Task<List<string?>> TitlesAsync(DbContextOptions<TAGDBContext> options, int? venueId = null, bool upcoming = false, int? limit = null)
    {
        await using var context = new TAGDBContext(options);
        var result = await new EventController(context).GetEvents(venueId, upcoming, limit);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return ((IEnumerable<object>)ok.Value!).Select(item => (string?)Prop(item, "Title")).ToList();
    }

    [Fact]
    public async Task Events_ForAVenue_ListOnlyThatVenuesPublishedEvents()
    {
        var options = await SeedEventsAsync(nameof(Events_ForAVenue_ListOnlyThatVenuesPublishedEvents));

        var titles = await TitlesAsync(options, venueId: 1);

        Assert.Equal(new[] { "Long gone", "Happening now", "Sooner", "Later" }, titles);
    }

    [Fact]
    public async Task Events_Upcoming_DropsFinishedOnes_KeepsTheOnesOnNow_AndOrdersByStart()
    {
        var options = await SeedEventsAsync(nameof(Events_Upcoming_DropsFinishedOnes_KeepsTheOnesOnNow_AndOrdersByStart));

        var titles = await TitlesAsync(options, venueId: 1, upcoming: true);

        Assert.Equal(new[] { "Happening now", "Sooner", "Later" }, titles);
    }

    [Fact]
    public async Task Events_Limit_IsAppliedAndClamped()
    {
        var options = await SeedEventsAsync(nameof(Events_Limit_IsAppliedAndClamped));

        Assert.Equal(new[] { "Happening now" }, await TitlesAsync(options, venueId: 1, upcoming: true, limit: 1));
        Assert.Single(await TitlesAsync(options, venueId: 1, upcoming: true, limit: 0));
        Assert.Equal(3, (await TitlesAsync(options, venueId: 1, upcoming: true, limit: 500)).Count);
    }

    [Fact]
    public async Task Events_Upcoming_WithoutAVenue_ListsEveryVenuesUpcomingEvents()
    {
        var options = await SeedEventsAsync(nameof(Events_Upcoming_WithoutAVenue_ListsEveryVenuesUpcomingEvents));

        var titles = await TitlesAsync(options, upcoming: true);

        Assert.Equal(new[] { "Happening now", "Sooner", "Elsewhere", "Later" }, titles);
    }

    [Fact]
    public async Task Events_ForAVenueWithNone_IsAnEmptyList()
    {
        var options = await SeedEventsAsync(nameof(Events_ForAVenueWithNone_IsAnEmptyList));

        Assert.Empty(await TitlesAsync(options, venueId: 999, upcoming: true));
    }

    [Fact]
    public async Task Events_WithoutFilters_StillListEveryPublishedEvent()
    {
        var options = await SeedEventsAsync(nameof(Events_WithoutFilters_StillListEveryPublishedEvent));

        var titles = await TitlesAsync(options);

        Assert.Equal(5, titles.Count);
        Assert.DoesNotContain("Draft", titles);
        Assert.DoesNotContain("Blocked", titles);
    }
}
