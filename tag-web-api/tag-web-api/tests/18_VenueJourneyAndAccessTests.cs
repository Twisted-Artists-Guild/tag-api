using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Controllers;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;
using Xunit;
using static tagApiUnitTests.VenueTestSupport;

namespace tagApiUnitTests;

// Story #340: the venue feature as a whole. One test follows an owner from sign-up to a public, listed venue with an event;
// the others pin down which venue and event endpoints anonymous callers can reach. Database names start with "venuejourney-"
// because they are shared across test classes.
public class VenueJourneyAndAccessTests
{
    private const int OwnerId = 10;
    private const int OtherUserId = 11;

    private static T SignedIn<T>(T controller, int? userId)
        where T : ControllerBase
    {
        var identity = userId.HasValue
            ? new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) }, "test")
            : new ClaimsIdentity();
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
        return controller;
    }

    private static int Status(object? result) => result switch
    {
        ObjectResult objectResult => objectResult.StatusCode ?? 200,
        StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
        _ => throw new InvalidOperationException($"Unexpected result {result?.GetType().Name}"),
    };

    private static async Task<VenueListPageDto> ListAsync(TAGDBContext context, string? q = null, string[]? category = null, string[]? tag = null, string[]? region = null, string? state = null)
    {
        var result = await SignedIn(new VenueListController(context), null).ListVenues(q, category, tag, region, null, state, null, null, VenueListController.DefaultPageSize);
        return Assert.IsType<VenueListPageDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    // ---- the whole journey

    [Fact]
    public async Task AnOwnerOnboardsAVenue_PublishesIt_AndThePublicFindsIt()
    {
        var options = CreateOptions("venuejourney-main");
        await using (var seed = new TAGDBContext(options))
        {
            seed.Set<VenueCategory>().AddRange(
                new VenueCategory { VenueCategoryID = 1, Name = "Theaters", Slug = "theaters", IsActive = true, SortOrder = 1 },
                new VenueCategory { VenueCategoryID = 2, Name = "Bars", Slug = "bars", IsActive = true, SortOrder = 2 });
            seed.Set<Region>().AddRange(
                new Region { RegionID = 1, Name = "Georgia", Slug = "georgia", Type = RegionTypes.Statewide, States = new() { "GA" }, IsActive = true },
                new Region { RegionID = 2, Name = "Florida", Slug = "florida", Type = RegionTypes.Statewide, States = new() { "FL" }, IsActive = true });
            await seed.SaveChangesAsync();
        }

        await using var context = new TAGDBContext(options);
        var owner = CreateController(context, OwnerId);
        var anonymous = CreateController(context, userId: null);

        // 1. Sign up: reserving the name creates a private venue that the signed-in user owns.
        var reserved = Assert.IsType<CreatedAtActionResult>((await owner.ReserveVenueSlug(new VenueSlugReservationRequest { Slug = "journey-hall", Title = "Journey Hall" })).Result);
        var venueId = Assert.IsType<VenueSlugReservationResponse>(reserved.Value).VenueID;
        Assert.True((await context.Set<Linker_UserToVenue>().AsNoTracking().AnyAsync(l => l.VenueID == venueId && l.UserID == OwnerId)));

        // 2. It is invisible to the public while it is a draft.
        Assert.IsType<NotFoundResult>((await anonymous.GetVenueBySlug("journey-hall")).Result);
        Assert.Empty((await ListAsync(context, q: "Journey")).Items);

        // 3. It can't be published before it has a real address.
        var draft = await context.Set<Venue>().AsNoTracking().SingleAsync(v => v.VenueID == venueId);
        VenueUpdateRequest Update(bool publish) => new()
        {
            VenueID = venueId,
            Name = draft.Name,
            AddressID = draft.AddressID,
            Description = "<p>A big room with a stage.</p>",
            IsPublished = publish,
        };
        Assert.Equal(400, Status(await owner.PutVenue(venueId, Update(true))));

        // 4. The owner fills in the address, categories, tags and regions.
        Assert.InRange(Status(await owner.PutVenueAddress(venueId, new VenueAddressRequest { AddressLine1 = "12 Peach St", City = "Savannah", State = "GA", ZipCode = "31401" })), 200, 299);
        Assert.Equal(200, Status((await SignedIn(new VenueCategoryController(context), OwnerId).SetVenueCategories(venueId, new VenueCategoriesRequest { CategoryIds = new() { 1 } })).Result));
        Assert.Equal(200, Status((await SignedIn(new VenueTagController(context), OwnerId).SetVenueTags(venueId, new VenueTagsRequest { Tags = new() { "Parking", "stage" } })).Result));
        Assert.Equal(200, Status((await SignedIn(new RegionController(context), OwnerId).SetVenueRegions(venueId, new VenueRegionsRequest { RegionIds = new() { 1 } })).Result));

        // 5. Nobody else can publish it, and it is still private.
        Assert.Equal(403, Status(await CreateController(context, OtherUserId).PutVenue(venueId, Update(true))));
        Assert.IsType<NotFoundResult>((await anonymous.GetVenueBySlug("journey-hall")).Result);

        // 6. The owner publishes it.
        Assert.Equal(204, Status(await owner.PutVenue(venueId, Update(true))));

        // 7. The public page shows it, with its details and the fact that someone manages it (not who).
        var page = Assert.IsType<OkObjectResult>((await anonymous.GetVenueBySlug("journey-hall")).Result);
        Assert.Equal("Journey Hall", Prop(page.Value, "Name"));
        Assert.Equal(true, Prop(page.Value, "HasOwner"));
        Assert.Equal("Savannah", ((Address)Prop(page.Value, "Address")!).City);
        Assert.Equal(new[] { "parking", "stage" }, ((IEnumerable<string>)Prop(page.Value, "Tags")!).ToArray());
        Assert.Null(Prop(page.Value, "Owners"));

        // 8. The directory lists it, and each filter finds it or leaves it out.
        Assert.Equal(new[] { venueId }, (await ListAsync(context, q: "Journey")).Items.Select(i => i.VenueID).ToArray());
        Assert.Contains(venueId, (await ListAsync(context, category: new[] { "theaters" })).Items.Select(i => i.VenueID));
        Assert.Contains(venueId, (await ListAsync(context, tag: new[] { "parking", "stage" })).Items.Select(i => i.VenueID));
        Assert.Contains(venueId, (await ListAsync(context, region: new[] { "georgia" })).Items.Select(i => i.VenueID));
        Assert.Contains(venueId, (await ListAsync(context, state: "ga")).Items.Select(i => i.VenueID));
        Assert.DoesNotContain(venueId, (await ListAsync(context, category: new[] { "bars" })).Items.Select(i => i.VenueID));
        Assert.DoesNotContain(venueId, (await ListAsync(context, tag: new[] { "parking", "rooftop" })).Items.Select(i => i.VenueID));
        Assert.DoesNotContain(venueId, (await ListAsync(context, region: new[] { "florida" })).Items.Select(i => i.VenueID));

        // 9. The owner hosts an event: it is private as a draft, and public once published.
        var events = SignedIn(new EventController(context), OwnerId);
        var start = DateTime.UtcNow.AddDays(14);
        var created = Assert.IsType<CreatedAtActionResult>(await events.CreateVenueEvent(venueId, new EventDetailsRequest
        {
            Title = "Opening Night",
            Description = "<p>Doors at seven.</p>",
            PointOfContact = "events@example.com",
            StartTime = start,
            EndTime = start.AddHours(3),
        }));
        var eventId = (int)Prop(created.Value, "EventID")!;
        var publicEvents = SignedIn(new EventController(context), null);

        Assert.Empty(await PublicEventIdsAsync(publicEvents, venueId));
        Assert.Equal(200, Status(await events.PublishEvent(eventId)));
        Assert.Equal(new[] { eventId }, await PublicEventIdsAsync(publicEvents, venueId));
        Assert.Equal(403, Status(await SignedIn(new EventController(context), OtherUserId).CancelEvent(eventId)));
    }

    private static async Task<int[]> PublicEventIdsAsync(EventController controller, int venueId)
    {
        var ok = Assert.IsType<OkObjectResult>((await controller.GetEvents(venueId, upcoming: true)).Result);
        return ((IEnumerable<object>)ok.Value!).Select(item => (int)Prop(item, "EventID")!).ToArray();
    }

    // ---- anonymous access rules

    // The actions a caller with no sign-in can reach: ones marked AllowAnonymous, and, where the controller itself does not
    // demand a sign-in, ones that carry no Authorize of their own.
    private static List<string> AnonymousActions(Type controller)
    {
        var classRequiresSignIn = controller.GetCustomAttribute<AuthorizeAttribute>() != null;
        var classAllowsAnonymous = controller.GetCustomAttribute<AllowAnonymousAttribute>() != null;

        return controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes().Any(attribute => attribute is HttpMethodAttribute))
            .Where(method => method.GetCustomAttribute<AllowAnonymousAttribute>() != null
                || (classAllowsAnonymous && method.GetCustomAttribute<AuthorizeAttribute>() == null)
                || (!classRequiresSignIn && method.GetCustomAttribute<AuthorizeAttribute>() == null))
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    public static IEnumerable<object[]> AnonymousRules() => new[]
    {
        new object[] { typeof(VenueController), new[] { nameof(VenueController.GetVenue), nameof(VenueController.GetVenueBySlug), nameof(VenueController.GetVenues) } },
        new object[] { typeof(VenueListController), new[] { nameof(VenueListController.ListVenues) } },
        new object[] { typeof(VenueCategoryController), new[] { nameof(VenueCategoryController.GetActiveCategories) } },
        new object[] { typeof(VenueTagController), new[] { nameof(VenueTagController.SuggestTags) } },
        new object[] { typeof(RegionController), new[] { nameof(RegionController.GetActiveRegions) } },
        new object[] { typeof(EventController), new[] { nameof(EventController.GetEvent), nameof(EventController.GetEventByPath), nameof(EventController.GetEvents) } },
        new object[] { typeof(VenueImportController), Array.Empty<string>() },
        new object[] { typeof(Linker_UserToVenueController), Array.Empty<string>() },
    };

    [Theory]
    [MemberData(nameof(AnonymousRules))]
    public void OnlyTheListedPublicReadsAreOpenToAnonymousCallers(Type controller, string[] allowed)
    {
        Assert.Equal(allowed.OrderBy(name => name, StringComparer.Ordinal), AnonymousActions(controller));
    }

    [Theory]
    [MemberData(nameof(AnonymousRules))]
    public void EveryChangeNeedsASignIn(Type controller, string[] allowed)
    {
        var anonymousChanges = controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes().Any(attribute => attribute is HttpPostAttribute or HttpPutAttribute or HttpPatchAttribute or HttpDeleteAttribute))
            .Select(method => method.Name)
            .Intersect(AnonymousActions(controller))
            .ToList();

        Assert.Empty(anonymousChanges);
        Assert.All(allowed, name => Assert.NotNull(controller.GetMethod(name)));
    }

    [Fact]
    public void TheAnonymousRulesCoverEveryVenueRelatedController()
    {
        var covered = AnonymousRules().Select(rule => (Type)rule[0]).ToHashSet();
        var venueControllers = typeof(VenueController).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract && type.Namespace == "TAGWEBAPI.Controllers")
            .Where(type => type.Name.Contains("Venue", StringComparison.Ordinal) && !type.Name.Contains("Linker_VenueTo", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(venueControllers);
        Assert.All(venueControllers, type => Assert.Contains(type, covered));
    }
}
