using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Controllers;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;
using Xunit;
using static tagApiUnitTests.VenueTestSupport;

namespace tagApiUnitTests;

// Story #335: the public, paged venue directory. Only published, non-blocked venues, alphabetical, filters that combine, keyset paging.
public class VenueListTests
{
    // Sorted by lower-case name: 3 aurora gallery, 7 Plain Venue, 4 Ryman Auditorium, 1 Terminal West, 2 The Earl, 8 The Earl.
    // 5 is a draft and 6 is blocked, so neither is ever listed. Venue 2 is owned; the others have no owner.
    private static readonly int[] AllListed = { 3, 7, 4, 1, 2, 8 };

    private static async Task<DbContextOptions<TAGDBContext>> SeedAsync(string databaseName)
    {
        var options = CreateOptions($"venuelist-{databaseName}");
        await using var seed = new TAGDBContext(options);

        seed.Set<Address>().AddRange(
            new Address { AddressID = 1, AddressLine1 = "887 W Marietta St", City = "Atlanta", State = "GA", ZipCode = "30318", Country = "USA" },
            new Address { AddressID = 2, AddressLine1 = "488 Flat Shoals Ave", City = "Atlanta", State = "Georgia", ZipCode = "30316", Country = "USA" },
            new Address { AddressID = 3, AddressLine1 = "1 Square", City = "Decatur", State = "GA", ZipCode = "30030", Country = "USA" },
            new Address { AddressID = 4, AddressLine1 = "116 5th Ave N", City = "Nashville", State = "TN", ZipCode = "37201", Country = "USA" },
            new Address { AddressID = 5, AddressLine1 = "5 Draft Rd", City = "Atlanta", State = "GA", ZipCode = "30318", Country = "USA" },
            new Address { AddressID = 6, AddressLine1 = "6 Blocked Rd", City = "Atlanta", State = "GA", ZipCode = "30318", Country = "USA" },
            new Address { AddressID = 7, AddressLine1 = "7 Plain St", City = "Atlanta", State = "GA", ZipCode = "30318", Country = "USA" },
            new Address { AddressID = 8, AddressLine1 = "8 College Ave", City = "Athens", State = "GA", ZipCode = "30601", Country = "USA" });

        seed.Pictures.Add(new Picture { PictureID = 50, URL = "https://cdn.example.com/terminal-west.jpg" });
        seed.Set<Venue>().AddRange(
            new Venue { VenueID = 1, Name = "Terminal West", AddressID = 1, IsPublished = true, LogoPicID = 50, Description = "<p>Big room</p>", Description_Plaintext = "Big room" },
            new Venue { VenueID = 2, Name = "The Earl", AddressID = 2, IsPublished = true },
            new Venue { VenueID = 3, Name = "aurora gallery", AddressID = 3, IsPublished = true },
            new Venue { VenueID = 4, Name = "Ryman Auditorium", AddressID = 4, IsPublished = true },
            new Venue { VenueID = 5, Name = "Draft Hall", AddressID = 5, IsPublished = false },
            new Venue { VenueID = 6, Name = "Blocked Bar", AddressID = 6, IsPublished = true, IsModerationBlocked = true },
            new Venue { VenueID = 7, Name = "<b>Plain</b> Venue", Name_Plaintext = "Plain Venue", AddressID = 7, IsPublished = true, Description_Plaintext = new string('x', 300) },
            new Venue { VenueID = 8, Name = "The Earl", AddressID = 8, IsPublished = true });
        seed.Set<Linker_UserToVenue>().Add(new Linker_UserToVenue { UserID = 10, VenueID = 2, Role = Linker_UserToVenue.OwnerRole });

        seed.Set<VenueCategory>().AddRange(
            new VenueCategory { VenueCategoryID = 1, Name = "Theaters", Slug = "theaters", SortOrder = 20 },
            new VenueCategory { VenueCategoryID = 2, Name = "Bars", Slug = "bars", SortOrder = 10 },
            new VenueCategory { VenueCategoryID = 3, Name = "Galleries", Slug = "galleries", SortOrder = 30 });
        seed.Set<Linker_VenueToCategory>().AddRange(
            new Linker_VenueToCategory { VenueID = 1, VenueCategoryID = 1 }, new Linker_VenueToCategory { VenueID = 1, VenueCategoryID = 2 },
            new Linker_VenueToCategory { VenueID = 2, VenueCategoryID = 2 },
            new Linker_VenueToCategory { VenueID = 3, VenueCategoryID = 3 },
            new Linker_VenueToCategory { VenueID = 4, VenueCategoryID = 1 },
            new Linker_VenueToCategory { VenueID = 5, VenueCategoryID = 1 });

        seed.Set<VenueTag>().AddRange(
            new VenueTag { VenueTagID = 1, Name = "parking" },
            new VenueTag { VenueTagID = 2, Name = "stage" },
            new VenueTag { VenueTagID = 3, Name = "wheelchair accessible" });
        seed.Set<Linker_VenueToTag>().AddRange(
            new Linker_VenueToTag { VenueID = 1, VenueTagID = 1 }, new Linker_VenueToTag { VenueID = 1, VenueTagID = 2 },
            new Linker_VenueToTag { VenueID = 2, VenueTagID = 1 },
            new Linker_VenueToTag { VenueID = 3, VenueTagID = 3 },
            new Linker_VenueToTag { VenueID = 4, VenueTagID = 1 }, new Linker_VenueToTag { VenueID = 4, VenueTagID = 2 }, new Linker_VenueToTag { VenueID = 4, VenueTagID = 3 });

        seed.Set<Region>().AddRange(
            new Region { RegionID = 1, Name = "Georgia", Slug = "georgia", Type = RegionTypes.Statewide, States = new() { "GA" } },
            new Region { RegionID = 2, Name = "Atlanta Metro", Slug = "atlanta-metro", Type = RegionTypes.WithinState, States = new() { "GA" } },
            new Region { RegionID = 3, Name = "Tennessee", Slug = "tennessee", Type = RegionTypes.Statewide, States = new() { "TN" } });
        seed.Set<Linker_VenueToRegion>().AddRange(
            new Linker_VenueToRegion { VenueID = 1, RegionID = 1 }, new Linker_VenueToRegion { VenueID = 1, RegionID = 2 },
            new Linker_VenueToRegion { VenueID = 2, RegionID = 1 }, new Linker_VenueToRegion { VenueID = 2, RegionID = 2 },
            new Linker_VenueToRegion { VenueID = 3, RegionID = 1 },
            new Linker_VenueToRegion { VenueID = 4, RegionID = 3 });

        await seed.SaveChangesAsync();
        return options;
    }

    private static async Task<VenueListPageDto> ListAsync(
        string databaseName,
        string? q = null,
        string[]? category = null,
        string[]? tag = null,
        string[]? region = null,
        string? city = null,
        string? state = null,
        string? zip = null,
        string? cursor = null,
        int limit = VenueListController.DefaultPageSize)
    {
        var options = await SeedAsync(databaseName);
        await using var context = new TAGDBContext(options);
        return await ListAsync(context, q, category, tag, region, city, state, zip, cursor, limit);
    }

    private static async Task<VenueListPageDto> ListAsync(
        TAGDBContext context,
        string? q = null,
        string[]? category = null,
        string[]? tag = null,
        string[]? region = null,
        string? city = null,
        string? state = null,
        string? zip = null,
        string? cursor = null,
        int limit = VenueListController.DefaultPageSize)
    {
        var result = await new VenueListController(context).ListVenues(q, category, tag, region, city, state, zip, cursor, limit);
        return Assert.IsType<VenueListPageDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    private static int[] Ids(VenueListPageDto page) => page.Items.Select(i => i.VenueID).ToArray();

    // ---- what is listed

    [Fact]
    public async Task List_ReturnsOnlyPublishedUnblockedVenues_AlphabeticallyIgnoringCase_WithOrWithoutAnOwner()
    {
        var page = await ListAsync(nameof(List_ReturnsOnlyPublishedUnblockedVenues_AlphabeticallyIgnoringCase_WithOrWithoutAnOwner));

        Assert.Equal(AllListed, Ids(page));
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task List_ItemsCarryTheCardDetails()
    {
        var page = await ListAsync(nameof(List_ItemsCarryTheCardDetails));
        var terminal = page.Items.Single(i => i.VenueID == 1);

        Assert.Equal("Terminal West", terminal.Name);
        Assert.Equal("terminal-west", terminal.Slug);
        Assert.Equal("Atlanta", terminal.City);
        Assert.Equal("GA", terminal.State);
        Assert.Equal("Big room", terminal.Description);
        Assert.Equal("https://cdn.example.com/terminal-west.jpg", terminal.LogoUrl);
        Assert.Equal(new[] { "Bars", "Theaters" }, terminal.Categories.Select(c => c.Name));
        Assert.Equal(new[] { "bars", "theaters" }, terminal.Categories.Select(c => c.Slug));
        Assert.Equal(new[] { "parking", "stage" }, terminal.Tags);
        Assert.Equal(new[] { "Atlanta Metro", "Georgia" }, terminal.Regions.Select(r => r.Name));
    }

    [Fact]
    public async Task List_ShowsThePlainTextNameAndATrimmedDescription_AndNothingPrivate()
    {
        var page = await ListAsync(nameof(List_ShowsThePlainTextNameAndATrimmedDescription_AndNothingPrivate));
        var plain = page.Items.Single(i => i.VenueID == 7);

        Assert.Equal("Plain Venue", plain.Name);
        Assert.Equal(201, plain.Description!.Length);
        Assert.EndsWith("…", plain.Description);
        Assert.Empty(plain.Categories);
        Assert.Empty(plain.Tags);
        Assert.Empty(plain.Regions);

        var fields = typeof(VenueListItemDto).GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "Categories", "City", "Description", "LogoUrl", "Name", "ProfilePicUrl", "Regions", "Slug", "State", "Tags", "VenueID" }, fields);
    }

    // ---- name search

    [Theory]
    [InlineData("EARL", new[] { 2, 8 })]
    [InlineData("the earl", new[] { 2, 8 })]
    [InlineData("  terminal ", new[] { 1 })]
    [InlineData("al", new[] { 3, 1 })]
    [InlineData("plain", new[] { 7 })]
    [InlineData("<b>", new int[0])]
    [InlineData("nothing like it", new int[0])]
    [InlineData("", new[] { 3, 7, 4, 1, 2, 8 })]
    [InlineData("   ", new[] { 3, 7, 4, 1, 2, 8 })]
    public async Task Search_MatchesAnywhereInTheNameIgnoringCaseAndMarkup(string q, int[] expected)
    {
        Assert.Equal(expected, Ids(await ListAsync($"{nameof(Search_MatchesAnywhereInTheNameIgnoringCaseAndMarkup)}-[{q}]", q: q)));
    }

    // ---- each filter alone

    [Theory]
    [InlineData("theaters", new[] { 4, 1 })]
    [InlineData("THEATERS", new[] { 4, 1 })]
    [InlineData("galleries", new[] { 3 })]
    [InlineData("no-such-category", new int[0])]
    public async Task Category_MatchesVenuesInThatCategory(string slug, int[] expected)
    {
        Assert.Equal(expected, Ids(await ListAsync($"{nameof(Category_MatchesVenuesInThatCategory)}-{slug}", category: new[] { slug })));
    }

    [Fact]
    public async Task Category_SeveralSlugsMatchAVenueInAnyOfThem()
    {
        var page = await ListAsync(nameof(Category_SeveralSlugsMatchAVenueInAnyOfThem), category: new[] { "theaters", "bars" });

        Assert.Equal(new[] { 4, 1, 2 }, Ids(page));
    }

    [Theory]
    [InlineData("parking", new[] { 4, 1, 2 })]
    [InlineData("  PARKING ", new[] { 4, 1, 2 })]
    [InlineData("wheelchair   accessible", new[] { 3, 4 })]
    [InlineData("nothing", new int[0])]
    [InlineData("<script>", new int[0])]
    public async Task Tag_MatchesVenuesThatHaveIt(string tag, int[] expected)
    {
        Assert.Equal(expected, Ids(await ListAsync($"{nameof(Tag_MatchesVenuesThatHaveIt)}-[{tag}]", tag: new[] { tag })));
    }

    [Fact]
    public async Task Tag_SeveralTagsNeedAVenueThatHasAllOfThem()
    {
        Assert.Equal(new[] { 4, 1 }, Ids(await ListAsync(nameof(Tag_SeveralTagsNeedAVenueThatHasAllOfThem) + "-a", tag: new[] { "parking", "stage" })));
        Assert.Equal(new[] { 4 }, Ids(await ListAsync(nameof(Tag_SeveralTagsNeedAVenueThatHasAllOfThem) + "-b", tag: new[] { "parking", "stage", "wheelchair accessible" })));
        Assert.Empty((await ListAsync(nameof(Tag_SeveralTagsNeedAVenueThatHasAllOfThem) + "-c", tag: new[] { "parking", "no such tag" })).Items);
    }

    [Theory]
    [InlineData("tennessee", new[] { 4 })]
    [InlineData("georgia", new[] { 3, 1, 2 })]
    [InlineData("ATLANTA-METRO", new[] { 1, 2 })]
    [InlineData("nowhere", new int[0])]
    public async Task Region_MatchesVenuesInThatRegion(string slug, int[] expected)
    {
        Assert.Equal(expected, Ids(await ListAsync($"{nameof(Region_MatchesVenuesInThatRegion)}-{slug}", region: new[] { slug })));
    }

    [Fact]
    public async Task Region_SeveralSlugsMatchAVenueInAnyOfThem()
    {
        var page = await ListAsync(nameof(Region_SeveralSlugsMatchAVenueInAnyOfThem), region: new[] { "tennessee", "atlanta-metro" });

        Assert.Equal(new[] { 4, 1, 2 }, Ids(page));
    }

    [Theory]
    [InlineData("Atlanta", new[] { 7, 1, 2 })]
    [InlineData("  ATLANTA ", new[] { 7, 1, 2 })]
    [InlineData("decatur", new[] { 3 })]
    [InlineData("atlan", new int[0])]
    public async Task City_MatchesTheWholeCityNameIgnoringCase(string city, int[] expected)
    {
        Assert.Equal(expected, Ids(await ListAsync($"{nameof(City_MatchesTheWholeCityNameIgnoringCase)}-{city.Trim()}", city: city)));
    }

    [Theory]
    [InlineData("GA", new[] { 3, 7, 1, 2, 8 })]
    [InlineData("ga", new[] { 3, 7, 1, 2, 8 })]
    [InlineData("Georgia", new[] { 3, 7, 1, 2, 8 })]
    [InlineData("TN", new[] { 4 })]
    [InlineData("tennessee", new[] { 4 })]
    [InlineData("XX", new int[0])]
    public async Task State_AcceptsACodeOrANameAndMatchesAddressesSavedEitherWay(string state, int[] expected)
    {
        Assert.Equal(expected, Ids(await ListAsync($"{nameof(State_AcceptsACodeOrANameAndMatchesAddressesSavedEitherWay)}-{state}", state: state)));
    }

    [Theory]
    [InlineData("303", new[] { 7, 1, 2 })]
    [InlineData("30318", new[] { 7, 1 })]
    [InlineData(" 30030 ", new[] { 3 })]
    [InlineData("37", new[] { 4 })]
    [InlineData("99999", new int[0])]
    [InlineData("30318-1234", new int[0])]
    public async Task Zip_MatchesByPrefix(string zip, int[] expected)
    {
        Assert.Equal(expected, Ids(await ListAsync($"{nameof(Zip_MatchesByPrefix)}-{zip.Trim()}", zip: zip)));
    }

    [Fact]
    public async Task Zip_WithNoDigitsIsIgnoredSoNoMarkupReachesTheQuery()
    {
        Assert.Equal(AllListed, Ids(await ListAsync(nameof(Zip_WithNoDigitsIsIgnoredSoNoMarkupReachesTheQuery), zip: "%_'; drop")));
    }

    // ---- filters together

    [Fact]
    public async Task Filters_CombineSoAVenueMustMatchAllOfThem()
    {
        Assert.Equal(new[] { 1 }, Ids(await ListAsync(nameof(Filters_CombineSoAVenueMustMatchAllOfThem) + "-1", category: new[] { "theaters" }, state: "GA")));
        Assert.Equal(new[] { 1, 2 }, Ids(await ListAsync(nameof(Filters_CombineSoAVenueMustMatchAllOfThem) + "-2", tag: new[] { "parking" }, city: "atlanta", region: new[] { "atlanta-metro" })));
        Assert.Equal(new[] { 8 }, Ids(await ListAsync(nameof(Filters_CombineSoAVenueMustMatchAllOfThem) + "-3", q: "earl", city: "athens")));
        Assert.Equal(new[] { 1 }, Ids(await ListAsync(nameof(Filters_CombineSoAVenueMustMatchAllOfThem) + "-4", q: "term", category: new[] { "bars" }, tag: new[] { "stage" }, region: new[] { "georgia" }, zip: "303", state: "georgia")));
        Assert.Empty((await ListAsync(nameof(Filters_CombineSoAVenueMustMatchAllOfThem) + "-5", category: new[] { "galleries" }, state: "TN")).Items);
        Assert.Empty((await ListAsync(nameof(Filters_CombineSoAVenueMustMatchAllOfThem) + "-6", q: "earl", tag: new[] { "stage" })).Items);
    }

    [Fact]
    public async Task Filters_NeverBringBackADraftOrABlockedVenue()
    {
        var page = await ListAsync(nameof(Filters_NeverBringBackADraftOrABlockedVenue), category: new[] { "theaters" }, city: "Atlanta");

        Assert.DoesNotContain(5, Ids(page));
        Assert.DoesNotContain(6, Ids(page));
        Assert.Equal(new[] { 1 }, Ids(page));
    }

    // ---- paging

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public async Task Paging_WalksTheWholeListWithoutRepeatingOrSkippingAnyVenue(int limit)
    {
        var options = await SeedAsync($"{nameof(Paging_WalksTheWholeListWithoutRepeatingOrSkippingAnyVenue)}-{limit}");
        await using var context = new TAGDBContext(options);

        var seen = new List<int>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var page = await ListAsync(context, cursor: cursor, limit: limit);
            Assert.True(page.Items.Count <= limit);
            seen.AddRange(Ids(page));
            cursor = page.NextCursor;
            pages++;
            Assert.True(pages <= 10, "paging did not finish");
        }
        while (cursor != null);

        Assert.Equal(AllListed, seen);
        Assert.Equal((int)Math.Ceiling(AllListed.Length / (double)limit), pages);
    }

    [Fact]
    public async Task Paging_TheLastPageHasNoCursor_AndAFullPageThatEndsTheListHasNoneEither()
    {
        var options = await SeedAsync(nameof(Paging_TheLastPageHasNoCursor_AndAFullPageThatEndsTheListHasNoneEither));
        await using var context = new TAGDBContext(options);

        var first = await ListAsync(context, limit: 3);
        Assert.NotNull(first.NextCursor);
        var second = await ListAsync(context, cursor: first.NextCursor, limit: 3);
        Assert.Equal(3, second.Items.Count);
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task Paging_VenuesWithTheSameNameAreSplitByIdAndNotLost()
    {
        var options = await SeedAsync(nameof(Paging_VenuesWithTheSameNameAreSplitByIdAndNotLost));
        await using var context = new TAGDBContext(options);

        var first = await ListAsync(context, limit: 5);
        Assert.Equal(new[] { 3, 7, 4, 1, 2 }, Ids(first));

        var second = await ListAsync(context, cursor: first.NextCursor, limit: 5);
        Assert.Equal(new[] { 8 }, Ids(second));
    }

    [Fact]
    public async Task Paging_KeepsTheFiltersApplied()
    {
        var options = await SeedAsync(nameof(Paging_KeepsTheFiltersApplied));
        await using var context = new TAGDBContext(options);

        var first = await ListAsync(context, category: new[] { "theaters", "bars" }, limit: 2);
        Assert.Equal(new[] { 4, 1 }, Ids(first));
        Assert.NotNull(first.NextCursor);

        var second = await ListAsync(context, category: new[] { "theaters", "bars" }, cursor: first.NextCursor, limit: 2);
        Assert.Equal(new[] { 2 }, Ids(second));
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task Paging_AVenueAddedMidWayAppearsLaterWithoutDisturbingThePagesAlreadyRead()
    {
        var options = await SeedAsync(nameof(Paging_AVenueAddedMidWayAppearsLaterWithoutDisturbingThePagesAlreadyRead));
        await using var context = new TAGDBContext(options);
        var first = await ListAsync(context, limit: 3);

        context.Set<Venue>().Add(new Venue { VenueID = 20, Name = "Zebra Room", AddressID = 1, IsPublished = true });
        context.Set<Venue>().Add(new Venue { VenueID = 21, Name = "Alpha Hall", AddressID = 1, IsPublished = true });
        await context.SaveChangesAsync();

        var second = await ListAsync(context, cursor: first.NextCursor, limit: 10);
        Assert.Equal(new[] { 1, 2, 8, 20 }, Ids(second));
        Assert.DoesNotContain(21, Ids(second));
    }

    [Theory]
    [InlineData("not a cursor")]
    [InlineData("%%%")]
    [InlineData("e30")]
    [InlineData("bnVsbA")]
    public async Task Paging_ABadCursorIsRejected(string cursor)
    {
        var options = await SeedAsync($"{nameof(Paging_ABadCursorIsRejected)}-{cursor}");
        await using var context = new TAGDBContext(options);

        var result = await new VenueListController(context).ListVenues(cursor: cursor);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Paging_ATooLongCursorIsRejected()
    {
        var options = await SeedAsync(nameof(Paging_ATooLongCursorIsRejected));
        await using var context = new TAGDBContext(options);

        var result = await new VenueListController(context).ListVenues(cursor: new string('a', 5000));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(2, 2)]
    [InlineData(100000, 6)]
    public async Task Limit_IsClamped(int limit, int expectedCount)
    {
        Assert.Equal(expectedCount, (await ListAsync($"{nameof(Limit_IsClamped)}-{limit}", limit: limit)).Items.Count);
    }

    [Fact]
    public async Task NoMatches_ReturnsAnEmptyPageWithNoCursor()
    {
        var page = await ListAsync(nameof(NoMatches_ReturnsAnEmptyPageWithNoCursor), q: "zzz");

        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task OverlongTextFiltersAreTrimmedInsteadOfFailing()
    {
        var page = await ListAsync(nameof(OverlongTextFiltersAreTrimmedInsteadOfFailing), q: new string('q', 5000), city: new string('c', 5000), state: new string('s', 5000));

        Assert.Empty(page.Items);
    }

    // ---- sorting (story #409)

    private static async Task<List<int>> SortedIdsAsync(string databaseName, string? sort, int limit)
    {
        var options = await SeedAsync(databaseName);
        await using var context = new TAGDBContext(options);
        var ids = new List<int>();
        string? cursor = null;
        do
        {
            var result = await new VenueListController(context).ListVenues(null, null, null, null, null, null, null, cursor, limit, sort);
            var page = Assert.IsType<VenueListPageDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
            ids.AddRange(page.Items.Select(i => i.VenueID));
            cursor = page.NextCursor;
        }
        while (cursor != null);
        return ids;
    }

    [Theory]
    [InlineData(null, new[] { 3, 7, 4, 1, 2, 8 })]
    [InlineData("name", new[] { 3, 7, 4, 1, 2, 8 })]
    [InlineData("nonsense", new[] { 3, 7, 4, 1, 2, 8 })]
    [InlineData("newest", new[] { 8, 7, 4, 3, 2, 1 })]
    [InlineData("OLDEST", new[] { 1, 2, 3, 4, 7, 8 })]
    [InlineData("category", new[] { 1, 2, 3, 4, 7, 8 })]
    public async Task Sort_OrdersTheList_AndPagingNeverRepeatsOrSkipsAVenue(string? sort, int[] expected)
    {
        Assert.Equal(expected, await SortedIdsAsync($"sort-all-{sort}", sort, 100));
        Assert.Equal(expected, await SortedIdsAsync($"sort-paged-{sort}", sort, 2));
        Assert.Equal(expected, await SortedIdsAsync($"sort-single-{sort}", sort, 1));
    }

    [Fact]
    public async Task ACursorFromOneSort_IsRefusedByAnother()
    {
        var options = await SeedAsync(nameof(ACursorFromOneSort_IsRefusedByAnother));
        await using var context = new TAGDBContext(options);
        var controller = new VenueListController(context);
        var first = Assert.IsType<VenueListPageDto>(Assert.IsType<OkObjectResult>((await controller.ListVenues(limit: 2, sort: "newest")).Result).Value);

        var result = await controller.ListVenues(cursor: first.NextCursor, limit: 2, sort: "name");

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task ItemsCarryTheProfilePicture_ForTheCard()
    {
        var options = await SeedAsync(nameof(ItemsCarryTheProfilePicture_ForTheCard));
        await using var context = new TAGDBContext(options);
        context.Pictures.Add(new Picture { PictureID = 51, URL = "https://cdn.example.com/profile.jpg" });
        context.Set<Venue>().Single(v => v.VenueID == 1).ProfilePicID = 51;
        await context.SaveChangesAsync();

        var page = Assert.IsType<VenueListPageDto>(Assert.IsType<OkObjectResult>((await new VenueListController(context).ListVenues()).Result).Value);

        Assert.Equal("https://cdn.example.com/profile.jpg", page.Items.Single(i => i.VenueID == 1).ProfilePicUrl);
        Assert.Equal("https://cdn.example.com/terminal-west.jpg", page.Items.Single(i => i.VenueID == 1).LogoUrl);
        Assert.Null(page.Items.Single(i => i.VenueID == 2).ProfilePicUrl);
    }}
