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

// Story #421: event hosts and role-based permissions. The owner (the event's artist, or the venue for a venue-hosted event)
// and platform staff have full control; collaborators added as hosts get a role. Database names start with "eventhost-".
public class EventHostTests
{
    private const int StaffId = 1;
    private const int VenueOwnerId = 10;
    private const int ArtistUserId = 20;
    private const int AdminUser = 31;
    private const int StaffUser = 32;
    private const int CheckInUser = 33;
    private const int ReadOnlyUser = 34;
    private const int StrangerId = 40;
    private const int OwnedVenue = 1;
    private const int ArtistId = 5;

    private static async Task<(DbContextOptions<TAGDBContext> Options, int EventId)> SeedAsync(string name, int? artistId = null)
    {
        var options = CreateOptions($"eventhost-{name}");
        await VenueTestSupport.SeedAsync(options, new Venue { VenueID = OwnedVenue, Name = "Owned Hall", AddressID = AddressId, IsPublished = true }, VenueOwnerId, StaffId);

        await using var seed = new TAGDBContext(options);
        foreach (var id in new[] { VenueOwnerId, ArtistUserId, AdminUser, StaffUser, CheckInUser, ReadOnlyUser, StrangerId })
        {
            seed.Users.Add(new User { UserID = id, EmailOne = $"u{id}@example.com", Username = $"user{id}" });
        }

        seed.Artists.Add(new Artist { ArtistID = ArtistId, Title = "Host Artist", Path = "host-artist", Applied = DateTime.UtcNow, Since = DateTime.UtcNow, IsPublished = true });
        seed.Set<Linker_UserToArtist>().Add(new Linker_UserToArtist { UserID = ArtistUserId, ArtistID = ArtistId, Role = "Owner" });

        var start = DateTime.UtcNow.AddDays(10);
        var @event = new Event
        {
            Title = "Class",
            Title_Plaintext = "Class",
            Description = "<p>Words</p>",
            Description_Plaintext = "Words",
            PointOfContact = "a@example.com",
            Path = $"class-{Guid.NewGuid():N}",
            ArtistID = artistId,
            VenueID = OwnedVenue,
            StartTime = start,
            EndTime = start.AddHours(2),
            Doors = start,
            StatusID = EventStatus.Draft,
        };
        seed.Events.Add(@event);
        seed.EventHosts.AddRange(
            new EventHost { EventID = 0, HostType = EventHost.UserType, HostID = AdminUser, Role = EventRole.Admin, Event = @event },
            new EventHost { EventID = 0, HostType = EventHost.UserType, HostID = StaffUser, Role = EventRole.Staff, Event = @event },
            new EventHost { EventID = 0, HostType = EventHost.UserType, HostID = CheckInUser, Role = EventRole.CheckIn, Event = @event },
            new EventHost { EventID = 0, HostType = EventHost.UserType, HostID = ReadOnlyUser, Role = EventRole.ReadOnly, Event = @event });
        await seed.SaveChangesAsync();
        return (options, @event.EventID);
    }

    private static ControllerContext ContextFor(int? userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(userId.HasValue
                ? new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) }, "test")
                : new ClaimsIdentity()),
        },
    };

    private static EventController Events(TAGDBContext context, int? userId) => new(context) { ControllerContext = ContextFor(userId) };

    private static EventHostController Hosts(TAGDBContext context, int? userId) => new(context) { ControllerContext = ContextFor(userId) };

    private static int Status(object? result) => result switch
    {
        IStatusCodeActionResult code => code.StatusCode ?? 200,
        _ => throw new InvalidOperationException($"Unexpected result {result?.GetType().Name}"),
    };

    private static async Task<int> GetEventStatusAsync(DbContextOptions<TAGDBContext> options, int eventId, int? userId)
    {
        await using var context = new TAGDBContext(options);
        return Status((await Events(context, userId).GetEvent(eventId)).Result);
    }

    private static async Task<int> EditStatusAsync(DbContextOptions<TAGDBContext> options, int eventId, int userId)
    {
        await using var context = new TAGDBContext(options);
        return Status(await Events(context, userId).UpdateEventDetails(eventId, new EventDetailsRequest { CheckInWindowMinutes = 45 }));
    }

    private static async Task<int> DeleteStatusAsync(DbContextOptions<TAGDBContext> options, int eventId, int userId)
    {
        await using var context = new TAGDBContext(options);
        return Status(await Events(context, userId).DeleteEvent(eventId));
    }

    private static async Task<int> AddAsync(DbContextOptions<TAGDBContext> options, int eventId, int userId, string type, int id, EventRole? role)
    {
        await using var context = new TAGDBContext(options);
        return Status(await Hosts(context, userId).AddHost(eventId, new EventHostRequest { HostType = type, HostID = id, Role = role }));
    }

    // ---- who can see a private draft, and what role they are told they have

    [Theory]
    [InlineData(VenueOwnerId, 200)]
    [InlineData(StaffId, 200)]
    [InlineData(AdminUser, 200)]
    [InlineData(StaffUser, 200)]
    [InlineData(CheckInUser, 200)]
    [InlineData(ReadOnlyUser, 200)]
    [InlineData(StrangerId, 404)]
    [InlineData(ArtistUserId, 404)]
    public async Task ADraft_IsOnlyVisibleToThePeopleInvolved(int userId, int expected)
    {
        var (options, eventId) = await SeedAsync($"{nameof(ADraft_IsOnlyVisibleToThePeopleInvolved)}-{userId}");

        Assert.Equal(expected, await GetEventStatusAsync(options, eventId, userId));
    }

    [Fact]
    public async Task ADraft_IsHiddenFromAnonymousVisitors()
    {
        var (options, eventId) = await SeedAsync(nameof(ADraft_IsHiddenFromAnonymousVisitors));

        Assert.Equal(404, await GetEventStatusAsync(options, eventId, null));
    }

    [Theory]
    [InlineData(VenueOwnerId, "Owner", true)]
    [InlineData(StaffId, "Owner", true)]
    [InlineData(AdminUser, "Admin", true)]
    [InlineData(StaffUser, "Staff", false)]
    [InlineData(CheckInUser, "CheckIn", false)]
    [InlineData(ReadOnlyUser, "ReadOnly", false)]
    public async Task TheEvent_TellsTheCallerTheirRole(int userId, string role, bool canManage)
    {
        var (options, eventId) = await SeedAsync($"{nameof(TheEvent_TellsTheCallerTheirRole)}-{userId}");
        await using var context = new TAGDBContext(options);

        var ok = Assert.IsType<OkObjectResult>((await Events(context, userId).GetEvent(eventId)).Result);

        Assert.Equal(role, Prop(ok.Value!, "MyRole"));
        Assert.Equal(canManage, Prop(ok.Value!, "CanManage"));
    }

    // ---- who can edit and who can delete

    [Theory]
    [InlineData(VenueOwnerId, 200)]
    [InlineData(StaffId, 200)]
    [InlineData(AdminUser, 200)]
    [InlineData(StaffUser, 403)]
    [InlineData(CheckInUser, 403)]
    [InlineData(ReadOnlyUser, 403)]
    [InlineData(StrangerId, 403)]
    public async Task EditingNeedsAtLeastTheAdminRole(int userId, int expected)
    {
        var (options, eventId) = await SeedAsync($"{nameof(EditingNeedsAtLeastTheAdminRole)}-{userId}");

        Assert.Equal(expected, await EditStatusAsync(options, eventId, userId));
    }

    [Theory]
    [InlineData(AdminUser, 403)]
    [InlineData(StaffUser, 403)]
    [InlineData(VenueOwnerId, 204)]
    public async Task DeletingNeedsTheOwner(int userId, int expected)
    {
        var (options, eventId) = await SeedAsync($"{nameof(DeletingNeedsTheOwner)}-{userId}");

        Assert.Equal(expected, await DeleteStatusAsync(options, eventId, userId));
    }

    [Fact]
    public async Task AnArtistsEvent_IsOwnedByTheArtistsUsers_NotTheVenue()
    {
        var (options, eventId) = await SeedAsync(nameof(AnArtistsEvent_IsOwnedByTheArtistsUsers_NotTheVenue), ArtistId);

        Assert.Equal(200, await EditStatusAsync(options, eventId, ArtistUserId));
        Assert.Equal(403, await EditStatusAsync(options, eventId, VenueOwnerId));
    }

    // ---- managing hosts

    [Fact]
    public async Task TheOwner_AddsAHost_WhoThenGetsTheRole()
    {
        var (options, eventId) = await SeedAsync(nameof(TheOwner_AddsAHost_WhoThenGetsTheRole));
        Assert.Equal(404, await GetEventStatusAsync(options, eventId, StrangerId));

        Assert.Equal(201, await AddAsync(options, eventId, VenueOwnerId, "user", StrangerId, EventRole.Admin));

        Assert.Equal(200, await EditStatusAsync(options, eventId, StrangerId));
    }

    [Theory]
    [InlineData(AdminUser)]
    [InlineData(StaffUser)]
    [InlineData(CheckInUser)]
    [InlineData(ReadOnlyUser)]
    public async Task OnlyTheOwnerCanChangeHosts(int userId)
    {
        var (options, eventId) = await SeedAsync($"{nameof(OnlyTheOwnerCanChangeHosts)}-{userId}");

        Assert.Equal(403, await AddAsync(options, eventId, userId, "User", StrangerId, EventRole.ReadOnly));
    }

    [Fact]
    public async Task AStranger_CannotSeeOrChangeHosts_AndGetsNotFound()
    {
        var (options, eventId) = await SeedAsync(nameof(AStranger_CannotSeeOrChangeHosts_AndGetsNotFound));
        await using var context = new TAGDBContext(options);

        Assert.Equal(404, Status(await Hosts(context, StrangerId).GetHosts(eventId)));
        Assert.Equal(404, await AddAsync(options, eventId, StrangerId, "User", StrangerId, EventRole.Admin));
    }

    [Fact]
    public async Task Staff_ManageTheHostsOfAnyEvent()
    {
        var (options, eventId) = await SeedAsync(nameof(Staff_ManageTheHostsOfAnyEvent));

        Assert.Equal(201, await AddAsync(options, eventId, StaffId, "User", StrangerId, EventRole.CheckIn));
    }

    [Fact]
    public async Task TheListShowsTheOwnerAndTheHosts_ToAnyoneInvolved()
    {
        var (options, eventId) = await SeedAsync(nameof(TheListShowsTheOwnerAndTheHosts_ToAnyoneInvolved));
        await using var context = new TAGDBContext(options);

        var ok = Assert.IsType<OkObjectResult>(await Hosts(context, ReadOnlyUser).GetHosts(eventId));

        Assert.Equal("ReadOnly", Prop(ok.Value!, "MyRole"));
        Assert.Equal(false, Prop(ok.Value!, "CanManageHosts"));
        Assert.Equal("Venue", Prop(Prop(ok.Value!, "Owner")!, "HostType"));
        Assert.Equal(4, ((System.Collections.IEnumerable)Prop(ok.Value!, "Hosts")!).Cast<object>().Count());
    }

    [Theory]
    [InlineData("User", 999, EventRole.Admin)]
    [InlineData("Artist", 999, EventRole.Admin)]
    [InlineData("Venue", 999, EventRole.Admin)]
    [InlineData("Band", 1, EventRole.Admin)]
    [InlineData("User", 0, EventRole.Admin)]
    [InlineData("User", StrangerId, EventRole.Owner)]
    [InlineData("User", StrangerId, EventRole.None)]
    [InlineData("User", StrangerId, null)]
    [InlineData("Venue", OwnedVenue, EventRole.Admin)]
    public async Task ABadHost_IsRejected(string type, int id, EventRole? role)
    {
        var (options, eventId) = await SeedAsync($"{nameof(ABadHost_IsRejected)}-{type}-{id}-{role}");

        Assert.Equal(400, await AddAsync(options, eventId, VenueOwnerId, type, id, role));
    }

    [Fact]
    public async Task TheSameHostTwice_IsAConflict()
    {
        var (options, eventId) = await SeedAsync(nameof(TheSameHostTwice_IsAConflict));

        Assert.Equal(409, await AddAsync(options, eventId, VenueOwnerId, "User", AdminUser, EventRole.ReadOnly));
    }

    [Fact]
    public async Task AnArtistHost_GivesItsUsersTheRole()
    {
        var (options, eventId) = await SeedAsync(nameof(AnArtistHost_GivesItsUsersTheRole));
        Assert.Equal(404, await GetEventStatusAsync(options, eventId, ArtistUserId));

        Assert.Equal(201, await AddAsync(options, eventId, VenueOwnerId, "Artist", ArtistId, EventRole.Admin));

        Assert.Equal(200, await EditStatusAsync(options, eventId, ArtistUserId));
    }

    [Fact]
    public async Task TheHighestRoleWins_WhenSomeoneHasSeveral()
    {
        var (options, eventId) = await SeedAsync(nameof(TheHighestRoleWins_WhenSomeoneHasSeveral));
        await AddAsync(options, eventId, VenueOwnerId, "Artist", ArtistId, EventRole.ReadOnly);
        await AddAsync(options, eventId, VenueOwnerId, "User", ArtistUserId, EventRole.Admin);

        Assert.Equal(200, await EditStatusAsync(options, eventId, ArtistUserId));
    }

    [Fact]
    public async Task ChangingARole_TakesEffectAtOnce_AndRemovingAHostEndsTheirAccess()
    {
        var (options, eventId) = await SeedAsync(nameof(ChangingARole_TakesEffectAtOnce_AndRemovingAHostEndsTheirAccess));
        int hostRowId;
        await using (var look = new TAGDBContext(options))
        {
            hostRowId = (await look.EventHosts.AsNoTracking().SingleAsync(h => h.HostID == ReadOnlyUser)).EventHostID;
        }

        await using (var context = new TAGDBContext(options))
        {
            var changed = await Hosts(context, VenueOwnerId).ChangeRole(eventId, hostRowId, new EventHostRequest { Role = EventRole.Admin });
            Assert.Equal(200, Status(changed));
        }

        Assert.Equal(200, await EditStatusAsync(options, eventId, ReadOnlyUser));

        await using (var context = new TAGDBContext(options))
        {
            Assert.Equal(204, Status(await Hosts(context, VenueOwnerId).RemoveHost(eventId, hostRowId)));
        }

        Assert.Equal(404, await GetEventStatusAsync(options, eventId, ReadOnlyUser));
    }

    [Fact]
    public async Task AHostRowOfAnotherEvent_CannotBeChanged()
    {
        var (options, eventId) = await SeedAsync(nameof(AHostRowOfAnotherEvent_CannotBeChanged));
        await using var context = new TAGDBContext(options);

        Assert.Equal(404, Status(await Hosts(context, VenueOwnerId).RemoveHost(eventId, 9999)));
    }

    [Fact]
    public async Task HostChanges_AreAudited()
    {
        var (options, eventId) = await SeedAsync(nameof(HostChanges_AreAudited));
        await AddAsync(options, eventId, VenueOwnerId, "User", StrangerId, EventRole.CheckIn);

        await using var check = new TAGDBContext(options);
        Assert.Contains(await check.Set<Log>().AsNoTracking().ToListAsync(), l => l.Tags.Contains("operation=host-add") && l.UserID == VenueOwnerId);
    }
}
