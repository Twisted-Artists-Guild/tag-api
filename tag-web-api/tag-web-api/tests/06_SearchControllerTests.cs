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

// The /search endpoints are anonymous, so they must only ever return public content and never private user fields.
public class SearchControllerTests
{
    private const int StaffId = 99;
    private const int MemberId = 10;

    private static SearchController CreateSearch(TAGDBContext context, int? userId)
    {
        var identity = userId.HasValue
            ? new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) }, "test")
            : new ClaimsIdentity();
        return new SearchController(context)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } },
        };
    }

    private static async Task<DbContextOptions<TAGDBContext>> SeedAsync(string databaseName)
    {
        var options = CreateOptions(databaseName);
        await VenueTestSupport.SeedAsync(options, null, null, StaffId);

        await using var seed = new TAGDBContext(options);
        seed.Users.AddRange(
            new User { UserID = MemberId, EmailOne = "ann.public@example.com", Username = "annpublic", FirstName = "Ann", FamName = "Seeker", IsPublished = true, BannedReason = "private note" },
            new User { UserID = 11, EmailOne = "hidden@example.com", Username = "annhidden", FirstName = "Ann", IsPublished = true, HideFromPublic = true },
            new User { UserID = 12, EmailOne = "unpublished@example.com", Username = "annunpublished", FirstName = "Ann", IsPublished = false },
            new User { UserID = 13, EmailOne = "blocked@example.com", Username = "annblocked", FirstName = "Ann", IsPublished = true, IsModerationBlocked = true });

        seed.Set<Venue>().AddRange(
            new Venue { VenueID = 1, Name = "Search Hall", Name_Plaintext = "Search Hall", AddressID = AddressId, IsPublished = true, Description = "<p>Big room</p>", Description_Plaintext = "Big room" },
            new Venue { VenueID = 2, Name = "Search Draft", AddressID = AddressId, IsPublished = false },
            new Venue { VenueID = 3, Name = "Search Blocked", AddressID = AddressId, IsPublished = true, IsModerationBlocked = true });

        seed.Artists.AddRange(
            new Artist { ArtistID = 1, Title = "Search Public", Path = "search-public", Applied = DateTime.UtcNow, Since = DateTime.UtcNow, IsPublished = true },
            new Artist { ArtistID = 2, Title = "Search Draft", Path = "search-draft", Applied = DateTime.UtcNow, Since = DateTime.UtcNow, IsPublished = false },
            new Artist { ArtistID = 3, Title = "Search Blocked", Path = "search-blocked", Applied = DateTime.UtcNow, Since = DateTime.UtcNow, IsPublished = true, IsModerationBlocked = true });

        seed.Events.AddRange(
            new Event { EventID = 1, ArtistID = 1, VenueID = 1, Title = "Search Live", Description = "d", PointOfContact = "p", Path = "search-live", StatusID = EventStatus.Published },
            new Event { EventID = 2, ArtistID = 1, VenueID = 1, Title = "Search Secret", Description = "d", PointOfContact = "p", Path = "search-secret", StatusID = EventStatus.Draft });

        await seed.SaveChangesAsync();
        return options;
    }

    private static IList<object> Items(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return ((System.Collections.IEnumerable)ok.Value!).Cast<object>().ToList();
    }

    // ---- users

    [Fact]
    public async Task User_Anonymous_IsRejected()
    {
        var options = await SeedAsync(nameof(User_Anonymous_IsRejected));
        await using var context = new TAGDBContext(options);

        var result = await CreateSearch(context, null).SearchUsers("ann");

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task User_Member_SeesOnlyPublicUsersWithoutPrivateFields()
    {
        var options = await SeedAsync(nameof(User_Member_SeesOnlyPublicUsersWithoutPrivateFields));
        await using var context = new TAGDBContext(options);

        var items = Items(await CreateSearch(context, MemberId).SearchUsers("ann"));

        var only = Assert.Single(items);
        Assert.Equal("annpublic", Prop(only, "Username"));
        Assert.Null(Prop(only, "EmailOne"));
        Assert.Null(only.GetType().GetProperty("BannedReason"));
        Assert.Null(only.GetType().GetProperty("BirthDate"));
    }

    [Fact]
    public async Task User_Member_CannotFindUsersByEmail()
    {
        var options = await SeedAsync(nameof(User_Member_CannotFindUsersByEmail));
        await using var context = new TAGDBContext(options);

        var items = Items(await CreateSearch(context, MemberId).SearchUsers("ann.public@example.com"));

        Assert.Empty(items);
    }

    [Fact]
    public async Task User_Staff_SeesEveryUserAndEmail()
    {
        var options = await SeedAsync(nameof(User_Staff_SeesEveryUserAndEmail));
        await using var context = new TAGDBContext(options);

        var byName = Items(await CreateSearch(context, StaffId).SearchUsers("ann"));
        Assert.Equal(4, byName.Count);
        Assert.All(byName, item => Assert.NotNull(Prop(item, "EmailOne")));

        var byEmail = Items(await CreateSearch(context, StaffId).SearchUsers("unpublished@example.com"));
        Assert.Single(byEmail);
    }

    // ---- venues

    [Fact]
    public async Task Venue_Anonymous_SeesOnlyPublishedUnblockedSummary()
    {
        var options = await SeedAsync(nameof(Venue_Anonymous_SeesOnlyPublishedUnblockedSummary));
        await using var context = new TAGDBContext(options);

        var items = Items(await CreateSearch(context, null).SearchVenues("Search"));

        var only = Assert.Single(items);
        Assert.Equal("Search Hall", Prop(only, "Name"));
        Assert.Equal("search-hall", Prop(only, "Slug"));
        Assert.Equal("Big room", Prop(only, "Description"));
        Assert.Null(only.GetType().GetProperty("IsPublished"));
        Assert.Null(only.GetType().GetProperty("EventSourceUrl"));
    }

    // ---- artists and events

    [Fact]
    public async Task Search_IgnoresCase()
    {
        var options = await SeedAsync(nameof(Search_IgnoresCase));
        await using var context = new TAGDBContext(options);
        var search = CreateSearch(context, StaffId);

        Assert.Single(Items(await search.SearchVenues("SEARCH HALL")));
        Assert.Single(Items(await search.SearchArtists("search public")));
        Assert.Single(Items(await search.SearchEvents("SEARCH LIVE")));
        Assert.Single(Items(await search.SearchUsers("ANNPUBLIC")));
        Assert.Empty(Items(await search.SearchVenues("nothing like it")));
    }

    [Fact]
    public async Task Artist_Anonymous_SeesOnlyPublishedUnblocked()
    {
        var options = await SeedAsync(nameof(Artist_Anonymous_SeesOnlyPublishedUnblocked));
        await using var context = new TAGDBContext(options);

        var items = Items(await CreateSearch(context, null).SearchArtists("Search"));

        var only = Assert.Single(items);
        Assert.Equal("Search Public", Prop(only, "Title"));
    }

    [Fact]
    public async Task Event_Anonymous_SeesOnlyPublished()
    {
        var options = await SeedAsync(nameof(Event_Anonymous_SeesOnlyPublished));
        await using var context = new TAGDBContext(options);

        var items = Items(await CreateSearch(context, null).SearchEvents("Search"));

        var only = Assert.Single(items);
        Assert.Equal("Search Live", Prop(only, "Title"));
    }

    // ---- limit

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(2, 2)]
    [InlineData(100000, 4)]
    public async Task Limit_IsClamped(int requested, int expected)
    {
        var options = await SeedAsync($"{nameof(Limit_IsClamped)}-{requested}");
        await using var context = new TAGDBContext(options);

        var items = Items(await CreateSearch(context, StaffId).SearchUsers("ann", requested));

        Assert.Equal(expected, items.Count);
    }
}
