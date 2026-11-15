using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Controllers;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;
using Xunit;
using static tagApiUnitTests.VenueTestSupport;

namespace tagApiUnitTests;

// Story #332: venues host and manage events. Who may create and change an event, the draft / publish / cancel lifecycle, and
// the venue's own event list. Database names start with "venueevent-" because they are shared across test classes.
public class VenueEventTests
{
    private const int StaffId = 1;
    private const int VenueOwnerId = 10;
    private const int ArtistUserId = 20;
    private const int StrangerId = 30;
    private const int OtherVenueOwnerId = 11;

    private const int OwnedVenue = 1;
    private const int UnownedVenue = 2;
    private const int OtherOwnersDraftVenue = 3;
    private const int ArtistId = 5;
    private const int OtherArtistId = 6;

    private static async Task<DbContextOptions<TAGDBContext>> SeedAsync(string name)
    {
        var options = CreateOptions($"venueevent-{name}");
        await VenueTestSupport.SeedAsync(options, new Venue { VenueID = OwnedVenue, Name = "Owned Hall", AddressID = AddressId, IsPublished = true }, VenueOwnerId, StaffId);

        await using var seed = new TAGDBContext(options);
        seed.Set<Venue>().AddRange(
            new Venue { VenueID = UnownedVenue, Name = "Unowned Hall", AddressID = AddressId, IsPublished = true },
            new Venue { VenueID = OtherOwnersDraftVenue, Name = "Draft Hall", AddressID = AddressId, IsPublished = false });
        seed.Set<Linker_UserToVenue>().Add(new Linker_UserToVenue { UserID = OtherVenueOwnerId, VenueID = OtherOwnersDraftVenue, Role = Linker_UserToVenue.OwnerRole });
        seed.Users.AddRange(
            new User { UserID = VenueOwnerId, EmailOne = "vo@example.com", Username = "venueowner" },
            new User { UserID = ArtistUserId, EmailOne = "au@example.com", Username = "artistuser" },
            new User { UserID = StrangerId, EmailOne = "st@example.com", Username = "stranger" },
            new User { UserID = OtherVenueOwnerId, EmailOne = "ov@example.com", Username = "othervenueowner" });
        seed.Artists.AddRange(
            new Artist { ArtistID = ArtistId, Title = "Host Artist", Path = "host-artist", Applied = DateTime.UtcNow, Since = DateTime.UtcNow, IsPublished = true },
            new Artist { ArtistID = OtherArtistId, Title = "Other Artist", Path = "other-artist", Applied = DateTime.UtcNow, Since = DateTime.UtcNow, IsPublished = true });
        seed.Set<Linker_UserToArtist>().Add(new Linker_UserToArtist { UserID = ArtistUserId, ArtistID = ArtistId, Role = "Owner" });
        await seed.SaveChangesAsync();
        return options;
    }

    private static EventController Controller(TAGDBContext context, int? userId)
    {
        var identity = userId.HasValue
            ? new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) }, "test")
            : new ClaimsIdentity();
        return new EventController(context)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } },
        };
    }

    private static int Status(object? result) => result switch
    {
        ObjectResult objectResult => objectResult.StatusCode ?? 200,
        StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
        _ => throw new InvalidOperationException($"Unexpected result {result?.GetType().Name}"),
    };

    private static EventDetailsRequest ValidRequest(string title = "Open Mic Night")
    {
        var start = DateTime.UtcNow.AddDays(10);
        return new()
        {
            Title = title,
            Description = "<p>Come and play.</p>",
            PointOfContact = "events@example.com",
            StartTime = start,
            EndTime = start.AddHours(3),
        };
    }

    private static async Task<int> AddEventAsync(
        DbContextOptions<TAGDBContext> options,
        int? artistId,
        EventStatus status,
        int venueId = OwnedVenue,
        string title = "Seeded",
        int daysFromNow = 10,
        bool complete = true)
    {
        await using var seed = new TAGDBContext(options);
        var start = DateTime.UtcNow.AddDays(daysFromNow);
        var @event = new Event
        {
            Title = title,
            Title_Plaintext = title,
            Description = complete ? "<p>Words</p>" : string.Empty,
            Description_Plaintext = complete ? "Words" : string.Empty,
            PointOfContact = complete ? "someone@example.com" : string.Empty,
            Path = $"seeded-{Guid.NewGuid():N}",
            ArtistID = artistId,
            VenueID = venueId,
            StartTime = start,
            EndTime = start.AddHours(3),
            Doors = start,
            StatusID = status,
        };
        seed.Events.Add(@event);
        await seed.SaveChangesAsync();
        return @event.EventID;
    }

    private static async Task<EventStatus> StatusOfAsync(DbContextOptions<TAGDBContext> options, int id)
    {
        await using var context = new TAGDBContext(options);
        return (await context.Events.AsNoTracking().SingleAsync(e => e.EventID == id)).StatusID;
    }

    private static async Task<List<object?>> PublicTitlesAsync(DbContextOptions<TAGDBContext> options)
    {
        await using var context = new TAGDBContext(options);
        var ok = Assert.IsType<OkObjectResult>((await Controller(context, null).GetEvents()).Result);
        return ((IEnumerable<object>)ok.Value!).Select(item => Prop(item, "Title")).ToList();
    }

    // ---- creating an event the venue hosts

    [Fact]
    public async Task AVenueOwner_CreatesADraft_HostedByTheVenue()
    {
        var options = await SeedAsync(nameof(AVenueOwner_CreatesADraft_HostedByTheVenue));
        await using var context = new TAGDBContext(options);

        var created = Assert.IsType<CreatedAtActionResult>(await Controller(context, VenueOwnerId).CreateVenueEvent(OwnedVenue, ValidRequest()));

        Assert.Equal("Draft", Prop(created.Value, "Status"));
        Assert.Null(Prop(created.Value, "ArtistID"));
        Assert.Equal("open-mic-night", Prop(created.Value, "Path"));
        Assert.Equal(true, Prop(created.Value, "CanManage"));
        Assert.Equal(OwnedVenue, Prop(created.Value, "VenueID"));
    }

    [Fact]
    public async Task ADraft_IsNotPublic()
    {
        var options = await SeedAsync(nameof(ADraft_IsNotPublic));
        await using (var context = new TAGDBContext(options))
        {
            await Controller(context, VenueOwnerId).CreateVenueEvent(OwnedVenue, ValidRequest("Hidden Show"));
        }

        Assert.DoesNotContain("Hidden Show", await PublicTitlesAsync(options));
    }

    [Fact]
    public async Task EventsWithTheSameTitle_GetDifferentPaths()
    {
        var options = await SeedAsync(nameof(EventsWithTheSameTitle_GetDifferentPaths));
        await using var context = new TAGDBContext(options);
        var controller = Controller(context, VenueOwnerId);

        var first = Assert.IsType<CreatedAtActionResult>(await controller.CreateVenueEvent(OwnedVenue, ValidRequest("Jam")));
        var second = Assert.IsType<CreatedAtActionResult>(await controller.CreateVenueEvent(OwnedVenue, ValidRequest("Jam")));

        Assert.Equal("jam", Prop(first.Value, "Path"));
        Assert.Equal("jam-2", Prop(second.Value, "Path"));
    }

    [Theory]
    [InlineData(StrangerId)]
    [InlineData(ArtistUserId)]
    [InlineData(OtherVenueOwnerId)]
    public async Task ANonOwner_CannotCreateAnEventHostedByTheVenue(int userId)
    {
        var options = await SeedAsync($"{nameof(ANonOwner_CannotCreateAnEventHostedByTheVenue)}{userId}");
        await using var context = new TAGDBContext(options);

        var result = await Controller(context, userId).CreateVenueEvent(OwnedVenue, ValidRequest());

        Assert.Equal(403, Status(result));
        Assert.Empty(context.Events);
    }

    [Fact]
    public async Task AnAnonymousCaller_CannotCreateAnEvent()
    {
        var options = await SeedAsync(nameof(AnAnonymousCaller_CannotCreateAnEvent));
        await using var context = new TAGDBContext(options);

        Assert.Equal(401, Status(await Controller(context, null).CreateVenueEvent(OwnedVenue, ValidRequest())));
    }

    [Fact]
    public async Task AnUnownedVenue_OnlyHasStaffManagedEvents()
    {
        var options = await SeedAsync(nameof(AnUnownedVenue_OnlyHasStaffManagedEvents));
        await using var context = new TAGDBContext(options);

        Assert.Equal(403, Status(await Controller(context, VenueOwnerId).CreateVenueEvent(UnownedVenue, ValidRequest())));
        Assert.Equal(201, Status(await Controller(context, StaffId).CreateVenueEvent(UnownedVenue, ValidRequest())));
    }

    [Fact]
    public async Task CreatingAtAVenueThatDoesNotExist_IsRefused()
    {
        var options = await SeedAsync(nameof(CreatingAtAVenueThatDoesNotExist_IsRefused));
        await using var context = new TAGDBContext(options);

        Assert.Equal(400, Status(await Controller(context, StaffId).CreateVenueEvent(999, ValidRequest())));
    }

    [Fact]
    public async Task CreatingADraft_NeedsATitleAndTimes_AndSaneValues()
    {
        var options = await SeedAsync(nameof(CreatingADraft_NeedsATitleAndTimes_AndSaneValues));
        await using var context = new TAGDBContext(options);
        var controller = Controller(context, VenueOwnerId);

        var noTitle = ValidRequest();
        noTitle.Title = "  ";
        var noTimes = ValidRequest();
        noTimes.StartTime = null;
        var endsBeforeItStarts = ValidRequest();
        endsBeforeItStarts.EndTime = endsBeforeItStarts.StartTime!.Value.AddHours(-1);
        var negativeCost = ValidRequest();
        negativeCost.Cost = -1;
        var absurdAge = ValidRequest();
        absurdAge.MinimumAge = 500;
        var longTitle = ValidRequest(new string('x', 256));

        foreach (var bad in new[] { noTitle, noTimes, endsBeforeItStarts, negativeCost, absurdAge, longTitle })
        {
            Assert.Equal(400, Status(await controller.CreateVenueEvent(OwnedVenue, bad)));
        }

        Assert.Empty(context.Events);
    }

    [Fact]
    public async Task ATitleWithMarkup_IsStoredAsText()
    {
        var options = await SeedAsync(nameof(ATitleWithMarkup_IsStoredAsText));
        await using var context = new TAGDBContext(options);

        var created = Assert.IsType<CreatedAtActionResult>(await Controller(context, VenueOwnerId).CreateVenueEvent(OwnedVenue, ValidRequest("<script>alert(1)</script> Jam")));

        Assert.Equal("<script>alert(1)</script> Jam", Prop(created.Value, "Title"));
        Assert.DoesNotContain("<script>", (string)Prop(created.Value, "TitleRichtext")!);
    }

    // ---- artists create events that tag a venue (existing create endpoint, now permission checked)

    [Fact]
    public async Task AnArtistUser_CreatesADraftForTheirArtist_TaggingAnyPublicVenue()
    {
        var options = await SeedAsync(nameof(AnArtistUser_CreatesADraftForTheirArtist_TaggingAnyPublicVenue));
        await using var context = new TAGDBContext(options);

        var posted = await Controller(context, ArtistUserId).PostEvent(NewEvent(ArtistId, UnownedVenue, EventStatus.Published));

        var created = Assert.IsType<CreatedAtActionResult>(posted.Result);
        var saved = Assert.IsType<Event>(created.Value);
        Assert.Equal(EventStatus.Draft, saved.StatusID);
        Assert.Equal(ArtistId, saved.ArtistID);
    }

    [Fact]
    public async Task AnArtistUser_CannotCreateForAnotherArtist_OrTagADraftVenueOfSomeoneElse()
    {
        var options = await SeedAsync(nameof(AnArtistUser_CannotCreateForAnotherArtist_OrTagADraftVenueOfSomeoneElse));
        await using var context = new TAGDBContext(options);
        var controller = Controller(context, ArtistUserId);

        Assert.Equal(403, Status((await controller.PostEvent(NewEvent(OtherArtistId, UnownedVenue))).Result));
        Assert.Equal(400, Status((await controller.PostEvent(NewEvent(ArtistId, OtherOwnersDraftVenue))).Result));
        Assert.Empty(context.Events);
    }

    [Fact]
    public async Task APost_WithNoArtist_IsAVenueHostedEvent_SoNeedsTheVenuesOwner()
    {
        var options = await SeedAsync(nameof(APost_WithNoArtist_IsAVenueHostedEvent_SoNeedsTheVenuesOwner));
        await using var context = new TAGDBContext(options);

        Assert.Equal(403, Status((await Controller(context, StrangerId).PostEvent(NewEvent(null, OwnedVenue))).Result));
        Assert.Equal(403, Status((await Controller(context, ArtistUserId).PostEvent(NewEvent(0, OwnedVenue))).Result));
        Assert.Equal(201, Status((await Controller(context, VenueOwnerId).PostEvent(NewEvent(null, OwnedVenue))).Result));
    }

    [Fact]
    public async Task APost_IgnoresAnIdAndNestedRecordsFromTheRequest()
    {
        var options = await SeedAsync(nameof(APost_IgnoresAnIdAndNestedRecordsFromTheRequest));
        await using var context = new TAGDBContext(options);
        var @event = NewEvent(ArtistId, UnownedVenue);
        @event.EventID = 777;
        @event.Artist = new Artist { ArtistID = 4242, Title = "Smuggled", Path = "smuggled", Applied = DateTime.UtcNow, Since = DateTime.UtcNow };

        Assert.Equal(201, Status((await Controller(context, ArtistUserId).PostEvent(@event)).Result));

        Assert.Equal(new[] { ArtistId }, await context.Events.Select(e => e.ArtistID!.Value).ToArrayAsync());
        Assert.DoesNotContain(await context.Artists.Select(a => a.ArtistID).ToListAsync(), id => id == 4242);
        Assert.DoesNotContain(await context.Events.Select(e => e.EventID).ToListAsync(), id => id == 777);
    }

    [Fact]
    public async Task APost_WithAPathThatIsTaken_IsAConflict()
    {
        var options = await SeedAsync(nameof(APost_WithAPathThatIsTaken_IsAConflict));
        await using var context = new TAGDBContext(options);
        var controller = Controller(context, ArtistUserId);
        Assert.Equal(201, Status((await controller.PostEvent(NewEvent(ArtistId, UnownedVenue))).Result));

        Assert.Equal(409, Status((await controller.PostEvent(NewEvent(ArtistId, UnownedVenue))).Result));
    }

    private static Event NewEvent(int? artistId, int venueId, EventStatus status = EventStatus.Draft) => new()
    {
        Title = "Posted",
        Description = "d",
        PointOfContact = "p",
        Path = "posted-event",
        ArtistID = artistId,
        VenueID = venueId,
        StartTime = DateTime.UtcNow.AddDays(5),
        EndTime = DateTime.UtcNow.AddDays(5).AddHours(2),
        Doors = DateTime.UtcNow.AddDays(5),
        StatusID = status,
    };

    // ---- who can change an event

    [Fact]
    public async Task AVenueOwner_EditsTheirOwnVenueHostedEvent()
    {
        var options = await SeedAsync(nameof(AVenueOwner_EditsTheirOwnVenueHostedEvent));
        var id = await AddEventAsync(options, null, EventStatus.Draft);
        await using var context = new TAGDBContext(options);

        var changed = new EventDetailsRequest { Title = "Renamed", Cost = 12.5m, MinimumAge = 18 };
        var ok = Assert.IsType<OkObjectResult>(await Controller(context, VenueOwnerId).UpdateEventDetails(id, changed));

        Assert.Equal("Renamed", Prop(ok.Value, "Title"));
        Assert.Equal(12.5m, Prop(ok.Value, "Cost"));
        Assert.Equal(18, Prop(ok.Value, "MinimumAge"));
        Assert.Equal("<p>Words</p>", Prop(ok.Value, "DescriptionRichtext"));
    }

    [Theory]
    [InlineData(StrangerId)]
    [InlineData(ArtistUserId)]
    [InlineData(OtherVenueOwnerId)]
    public async Task ANonOwner_CannotChangeAVenueHostedEvent(int userId)
    {
        var options = await SeedAsync($"{nameof(ANonOwner_CannotChangeAVenueHostedEvent)}{userId}");
        var id = await AddEventAsync(options, null, EventStatus.Draft);
        await using var context = new TAGDBContext(options);
        var controller = Controller(context, userId);

        Assert.Equal(403, Status(await controller.UpdateEventDetails(id, new EventDetailsRequest { Title = "Hijacked" })));
        Assert.Equal(403, Status(await controller.PublishEvent(id)));
        Assert.Equal(403, Status(await controller.CancelEvent(id)));
        Assert.Equal(403, Status(await controller.DeleteEvent(id)));
        Assert.Equal(403, Status(await controller.PutEvent(id, await LoadAsync(options, id))));
        Assert.Equal("Seeded", (await context.Events.AsNoTracking().SingleAsync(e => e.EventID == id)).Title);
    }

    [Fact]
    public async Task AnArtistsEventAtAnOwnedVenue_IsViewOnlyForTheVenueOwner()
    {
        var options = await SeedAsync(nameof(AnArtistsEventAtAnOwnedVenue_IsViewOnlyForTheVenueOwner));
        var id = await AddEventAsync(options, ArtistId, EventStatus.Published);
        await using var context = new TAGDBContext(options);
        var owner = Controller(context, VenueOwnerId);

        Assert.Equal(403, Status(await owner.UpdateEventDetails(id, new EventDetailsRequest { Title = "Mine now" })));
        Assert.Equal(403, Status(await owner.CancelEvent(id)));
        Assert.Equal(403, Status(await owner.UnpublishEvent(id)));
        Assert.Equal(403, Status(await owner.DeleteEvent(id)));

        var artistEdit = Assert.IsType<OkObjectResult>(await Controller(context, ArtistUserId).UpdateEventDetails(id, new EventDetailsRequest { Title = "Theirs" }));
        Assert.Equal("Theirs", Prop(artistEdit.Value, "Title"));
    }

    [Fact]
    public async Task APut_CannotChangeTheStatusOrTheArtist_ButStaffCan()
    {
        var options = await SeedAsync(nameof(APut_CannotChangeTheStatusOrTheArtist_ButStaffCan));
        var id = await AddEventAsync(options, null, EventStatus.Draft);

        await using (var context = new TAGDBContext(options))
        {
            var body = await LoadAsync(options, id);
            body.StatusID = EventStatus.Published;
            body.ArtistID = OtherArtistId;
            body.Title = "Edited";
            Assert.Equal(204, Status(await Controller(context, VenueOwnerId).PutEvent(id, body)));
        }

        await using (var context = new TAGDBContext(options))
        {
            var saved = await context.Events.AsNoTracking().SingleAsync(e => e.EventID == id);
            Assert.Equal("Edited", saved.Title);
            Assert.Equal(EventStatus.Draft, saved.StatusID);
            Assert.Null(saved.ArtistID);
        }

        await using (var context = new TAGDBContext(options))
        {
            var body = await LoadAsync(options, id);
            body.StatusID = EventStatus.Published;
            body.ArtistID = OtherArtistId;
            Assert.Equal(204, Status(await Controller(context, StaffId).PutEvent(id, body)));
        }

        await using var check = new TAGDBContext(options);
        var staffSaved = await check.Events.AsNoTracking().SingleAsync(e => e.EventID == id);
        Assert.Equal(EventStatus.Published, staffSaved.StatusID);
        Assert.Equal(OtherArtistId, staffSaved.ArtistID);
    }

    [Fact]
    public async Task APut_CannotMoveAnEventToAVenueTheUserCannotHostAt()
    {
        var options = await SeedAsync(nameof(APut_CannotMoveAnEventToAVenueTheUserCannotHostAt));
        var id = await AddEventAsync(options, null, EventStatus.Draft);
        await using var context = new TAGDBContext(options);
        var body = await LoadAsync(options, id);
        body.VenueID = UnownedVenue;

        Assert.Equal(403, Status(await Controller(context, VenueOwnerId).PutEvent(id, body)));
    }

    private static async Task<Event> LoadAsync(DbContextOptions<TAGDBContext> options, int id)
    {
        await using var context = new TAGDBContext(options);
        return await context.Events.AsNoTracking().SingleAsync(e => e.EventID == id);
    }

    [Fact]
    public async Task ACancelledEvent_CanNoLongerBeEdited_ExceptByStaff()
    {
        var options = await SeedAsync(nameof(ACancelledEvent_CanNoLongerBeEdited_ExceptByStaff));
        var id = await AddEventAsync(options, null, EventStatus.Cancelled);
        await using var context = new TAGDBContext(options);

        Assert.Equal(409, Status(await Controller(context, VenueOwnerId).UpdateEventDetails(id, new EventDetailsRequest { Title = "Revived" })));
        Assert.Equal(200, Status(await Controller(context, StaffId).UpdateEventDetails(id, new EventDetailsRequest { Title = "Staff fix" })));
    }

    [Fact]
    public async Task APublishedEvent_HasToStayComplete()
    {
        var options = await SeedAsync(nameof(APublishedEvent_HasToStayComplete));
        var id = await AddEventAsync(options, null, EventStatus.Published);
        await using var context = new TAGDBContext(options);

        var result = await Controller(context, VenueOwnerId).UpdateEventDetails(id, new EventDetailsRequest { Description = "" });

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Add a description.", (IEnumerable<string>)Prop(bad.Value, "problems")!);
        Assert.Equal("<p>Words</p>", (await LoadAsync(options, id)).Description);
    }

    [Fact]
    public async Task AnUpdate_WhoseEndIsBeforeItsStart_IsRefused()
    {
        var options = await SeedAsync(nameof(AnUpdate_WhoseEndIsBeforeItsStart_IsRefused));
        var id = await AddEventAsync(options, null, EventStatus.Draft);
        await using var context = new TAGDBContext(options);

        var result = await Controller(context, VenueOwnerId).UpdateEventDetails(id, new EventDetailsRequest { EndTime = DateTime.UtcNow.AddDays(-30) });

        Assert.Equal(400, Status(result));
    }

    // ---- publish, unpublish, cancel

    [Fact]
    public async Task APublish_NeedsTheMinimumInformation()
    {
        var options = await SeedAsync(nameof(APublish_NeedsTheMinimumInformation));
        var id = await AddEventAsync(options, null, EventStatus.Draft, complete: false);
        await using var context = new TAGDBContext(options);

        var result = await Controller(context, VenueOwnerId).PublishEvent(id);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var problems = ((IEnumerable<string>)Prop(bad.Value, "problems")!).ToList();
        Assert.Contains("Add a description.", problems);
        Assert.Contains("Add a point of contact.", problems);
        Assert.Equal(EventStatus.Draft, await StatusOfAsync(options, id));
    }

    [Fact]
    public async Task APublish_RefusesAnEventThatHasAlreadyEnded_OrEndsBeforeItStarts()
    {
        var options = await SeedAsync(nameof(APublish_RefusesAnEventThatHasAlreadyEnded_OrEndsBeforeItStarts));
        var ended = await AddEventAsync(options, null, EventStatus.Draft, daysFromNow: -30);
        await using var context = new TAGDBContext(options);

        var bad = Assert.IsType<BadRequestObjectResult>(await Controller(context, VenueOwnerId).PublishEvent(ended));

        Assert.Contains("The event has already ended.", (IEnumerable<string>)Prop(bad.Value, "problems")!);
    }

    [Fact]
    public async Task AVenueOwner_PublishesUnpublishesAndCancels_TheirEvent()
    {
        var options = await SeedAsync(nameof(AVenueOwner_PublishesUnpublishesAndCancels_TheirEvent));
        var id = await AddEventAsync(options, null, EventStatus.Draft, title: "Public Show");
        await using var context = new TAGDBContext(options);
        var owner = Controller(context, VenueOwnerId);

        Assert.Equal(200, Status(await owner.PublishEvent(id)));
        Assert.Equal(EventStatus.Published, await StatusOfAsync(options, id));
        Assert.Contains("Public Show", await PublicTitlesAsync(options));

        Assert.Equal(200, Status(await owner.UnpublishEvent(id)));
        Assert.Equal(EventStatus.Draft, await StatusOfAsync(options, id));
        Assert.DoesNotContain("Public Show", await PublicTitlesAsync(options));

        Assert.Equal(200, Status(await owner.PublishEvent(id)));
        Assert.Equal(200, Status(await owner.CancelEvent(id)));
        Assert.Equal(EventStatus.Cancelled, await StatusOfAsync(options, id));
        Assert.DoesNotContain("Public Show", await PublicTitlesAsync(options));
    }

    [Fact]
    public async Task PublishingTwice_IsHarmless()
    {
        var options = await SeedAsync(nameof(PublishingTwice_IsHarmless));
        var id = await AddEventAsync(options, null, EventStatus.Draft);
        await using var context = new TAGDBContext(options);
        var owner = Controller(context, VenueOwnerId);

        Assert.Equal(200, Status(await owner.PublishEvent(id)));
        Assert.Equal(200, Status(await owner.PublishEvent(id)));
    }

    [Theory]
    [InlineData(EventStatus.Cancelled)]
    [InlineData(EventStatus.Archived)]
    [InlineData(EventStatus.ModerationBlocked)]
    public async Task AnEventThatIsOver_OrBlocked_CannotBePublishedAgainByItsOwner(EventStatus status)
    {
        var options = await SeedAsync($"{nameof(AnEventThatIsOver_OrBlocked_CannotBePublishedAgainByItsOwner)}{status}");
        var id = await AddEventAsync(options, null, status);
        await using var context = new TAGDBContext(options);

        Assert.Equal(409, Status(await Controller(context, VenueOwnerId).PublishEvent(id)));
        Assert.Equal(status, await StatusOfAsync(options, id));
    }

    [Fact]
    public async Task APublish_NeedsAPublicVenue()
    {
        var options = await SeedAsync(nameof(APublish_NeedsAPublicVenue));
        var id = await AddEventAsync(options, null, EventStatus.Draft, venueId: OtherOwnersDraftVenue);
        await using var context = new TAGDBContext(options);

        var bad = Assert.IsType<BadRequestObjectResult>(await Controller(context, OtherVenueOwnerId).PublishEvent(id));

        Assert.Contains("Publish the venue before publishing its events.", (IEnumerable<string>)Prop(bad.Value, "problems")!);
    }

    [Fact]
    public async Task Staff_CanForceCancelAndUnpublishAnyEvent()
    {
        var options = await SeedAsync(nameof(Staff_CanForceCancelAndUnpublishAnyEvent));
        var artistEvent = await AddEventAsync(options, ArtistId, EventStatus.Published, title: "Disputed");
        var otherEvent = await AddEventAsync(options, OtherArtistId, EventStatus.Published, venueId: UnownedVenue, title: "Pulled");
        await using var context = new TAGDBContext(options);
        var staff = Controller(context, StaffId);

        Assert.Equal(200, Status(await staff.CancelEvent(artistEvent)));
        Assert.Equal(200, Status(await staff.UnpublishEvent(otherEvent)));

        Assert.Equal(EventStatus.Cancelled, await StatusOfAsync(options, artistEvent));
        Assert.Equal(EventStatus.Draft, await StatusOfAsync(options, otherEvent));
        Assert.DoesNotContain("Disputed", await PublicTitlesAsync(options));
        Assert.DoesNotContain("Pulled", await PublicTitlesAsync(options));
    }

    [Fact]
    public async Task EveryStatusChange_IsWrittenToTheAuditLog()
    {
        var options = await SeedAsync(nameof(EveryStatusChange_IsWrittenToTheAuditLog));
        var id = await AddEventAsync(options, null, EventStatus.Draft);
        await using var context = new TAGDBContext(options);
        var owner = Controller(context, VenueOwnerId);

        await owner.PublishEvent(id);
        await owner.CancelEvent(id);

        var logs = await context.Set<Log>().Where(l => l.Tags!.Contains("entity=event")).ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.All(logs, log => Assert.Equal(VenueOwnerId, log.UserID));
        Assert.Contains(logs, log => log.LoggedData!.Contains("from=Published;to=Cancelled"));
    }

    // ---- who can read an event

    [Fact]
    public async Task ADraft_IsHiddenFromEveryoneButItsManagers()
    {
        var options = await SeedAsync(nameof(ADraft_IsHiddenFromEveryoneButItsManagers));
        var id = await AddEventAsync(options, null, EventStatus.Draft);
        await using var context = new TAGDBContext(options);

        Assert.IsType<NotFoundResult>((await Controller(context, null).GetEvent(id)).Result);
        Assert.IsType<NotFoundResult>((await Controller(context, StrangerId).GetEvent(id)).Result);
        Assert.IsType<NotFoundResult>((await Controller(context, ArtistUserId).GetEvent(id)).Result);

        var owner = Assert.IsType<OkObjectResult>((await Controller(context, VenueOwnerId).GetEvent(id)).Result);
        Assert.Equal(true, Prop(owner.Value, "CanManage"));
        Assert.IsType<OkObjectResult>((await Controller(context, StaffId).GetEvent(id)).Result);
    }

    [Fact]
    public async Task APublishedEvent_IsPublic_AndSaysWhetherTheCallerCanManageIt()
    {
        var options = await SeedAsync(nameof(APublishedEvent_IsPublic_AndSaysWhetherTheCallerCanManageIt));
        var id = await AddEventAsync(options, ArtistId, EventStatus.Published);
        await using var context = new TAGDBContext(options);

        var anonymous = Assert.IsType<OkObjectResult>((await Controller(context, null).GetEvent(id)).Result);
        var venueOwner = Assert.IsType<OkObjectResult>((await Controller(context, VenueOwnerId).GetEvent(id)).Result);
        var artist = Assert.IsType<OkObjectResult>((await Controller(context, ArtistUserId).GetEvent(id)).Result);

        Assert.Equal(false, Prop(anonymous.Value, "CanManage"));
        Assert.Equal(false, Prop(venueOwner.Value, "CanManage"));
        Assert.Equal(true, Prop(artist.Value, "CanManage"));
    }

    // ---- the venue's event list

    [Fact]
    public async Task TheVenueList_ShowsOwnEventsInAnyState_AndOthersOnlyOncePublished()
    {
        var options = await SeedAsync(nameof(TheVenueList_ShowsOwnEventsInAnyState_AndOthersOnlyOncePublished));
        await AddEventAsync(options, null, EventStatus.Draft, title: "Own draft", daysFromNow: 3);
        await AddEventAsync(options, null, EventStatus.Cancelled, title: "Own cancelled", daysFromNow: 4);
        await AddEventAsync(options, null, EventStatus.Published, title: "Own past", daysFromNow: -20);
        await AddEventAsync(options, ArtistId, EventStatus.Published, title: "Artist published", daysFromNow: 5);
        await AddEventAsync(options, ArtistId, EventStatus.Draft, title: "Artist draft", daysFromNow: 6);
        await AddEventAsync(options, null, EventStatus.ModerationBlocked, title: "Own blocked", daysFromNow: 7);
        await AddEventAsync(options, null, EventStatus.Draft, venueId: UnownedVenue, title: "Elsewhere", daysFromNow: 8);
        await using var context = new TAGDBContext(options);

        var ok = Assert.IsType<OkObjectResult>((await Controller(context, VenueOwnerId).GetVenueEventsForManagement(OwnedVenue)).Result);
        var rows = ((IEnumerable<object>)ok.Value!).ToList();

        Assert.Equal(
            new[] { "Artist published", "Own cancelled", "Own draft", "Own past" },
            rows.Select(row => (string)Prop(row, "Title")!).OrderBy(title => title).ToArray());
        Assert.Equal(false, Prop(rows.Single(row => (string)Prop(row, "Title")! == "Artist published"), "CanManage"));
        Assert.Equal(true, Prop(rows.Single(row => (string)Prop(row, "Title")! == "Own draft"), "CanManage"));
    }

    [Fact]
    public async Task TheVenueList_IsOrderedNewestFirst()
    {
        var options = await SeedAsync(nameof(TheVenueList_IsOrderedNewestFirst));
        await AddEventAsync(options, null, EventStatus.Draft, title: "Soon", daysFromNow: 2);
        await AddEventAsync(options, null, EventStatus.Draft, title: "Later", daysFromNow: 20);
        await AddEventAsync(options, null, EventStatus.Draft, title: "Past", daysFromNow: -5);
        await using var context = new TAGDBContext(options);

        var ok = Assert.IsType<OkObjectResult>((await Controller(context, VenueOwnerId).GetVenueEventsForManagement(OwnedVenue)).Result);

        Assert.Equal(new[] { "Later", "Soon", "Past" }, ((IEnumerable<object>)ok.Value!).Select(row => (string)Prop(row, "Title")!).ToArray());
    }

    [Fact]
    public async Task TheVenueList_IsForTheVenuesManagersOnly_AndStaffSeeEverything()
    {
        var options = await SeedAsync(nameof(TheVenueList_IsForTheVenuesManagersOnly_AndStaffSeeEverything));
        await AddEventAsync(options, ArtistId, EventStatus.Draft, title: "Artist draft");
        await AddEventAsync(options, null, EventStatus.ModerationBlocked, title: "Own blocked");
        await using var context = new TAGDBContext(options);

        Assert.Equal(403, Status((await Controller(context, StrangerId).GetVenueEventsForManagement(OwnedVenue)).Result));
        Assert.Equal(403, Status((await Controller(context, ArtistUserId).GetVenueEventsForManagement(OwnedVenue)).Result));
        Assert.Equal(401, Status((await Controller(context, null).GetVenueEventsForManagement(OwnedVenue)).Result));

        var staff = Assert.IsType<OkObjectResult>((await Controller(context, StaffId).GetVenueEventsForManagement(OwnedVenue)).Result);
        Assert.Equal(2, ((IEnumerable<object>)staff.Value!).Count());
    }

    // ---- deleting and slugs

    [Fact]
    public async Task OnlyADraft_CanBeDeletedByItsOwner()
    {
        var options = await SeedAsync(nameof(OnlyADraft_CanBeDeletedByItsOwner));
        var draft = await AddEventAsync(options, null, EventStatus.Draft);
        var published = await AddEventAsync(options, null, EventStatus.Published);
        await using var context = new TAGDBContext(options);
        var owner = Controller(context, VenueOwnerId);

        Assert.Equal(409, Status(await owner.DeleteEvent(published)));
        Assert.Equal(204, Status(await owner.DeleteEvent(draft)));
        Assert.Equal(204, Status(await Controller(context, StaffId).DeleteEvent(published)));
    }

    [Fact]
    public async Task TheSlugEndpoints_NeedPermissionToo()
    {
        var options = await SeedAsync(nameof(TheSlugEndpoints_NeedPermissionToo));
        var id = await AddEventAsync(options, null, EventStatus.Draft);
        await using var context = new TAGDBContext(options);
        var reservation = new EventSlugReservationRequest { Slug = "reserved-slug", VenueID = OwnedVenue };

        Assert.Equal(403, Status((await Controller(context, StrangerId).ReserveEventSlug(reservation)).Result));
        Assert.Equal(403, Status((await Controller(context, StrangerId).UpdateEventSlug(id, new EventSlugUpdateRequest { Slug = "stolen" })).Result));
        Assert.Equal(201, Status((await Controller(context, VenueOwnerId).ReserveEventSlug(reservation)).Result));
        Assert.Equal(409, Status((await Controller(context, VenueOwnerId).ReserveEventSlug(reservation)).Result));
        Assert.Equal(200, Status((await Controller(context, VenueOwnerId).UpdateEventSlug(id, new EventSlugUpdateRequest { Slug = "my-new-slug" })).Result));
    }
}
