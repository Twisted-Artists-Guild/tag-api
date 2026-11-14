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

// Story #326: free-form amenity tags. Normalized, validated, suggested by popularity, filterable, staff can merge and remove.
public class VenueTagTests
{
    private const int StaffId = 99;
    private const int OwnerId = 10;
    private const int OtherUserId = 11;

    // Venues: 1 (published, owned by OwnerId), 2 (published), 3 (draft).
    // Tags: 1 parking (venues 1,2,3), 2 stage (1,2), 3 wheelchair accessible (1), 4 parking lot (2), 5 orphan (none).
    private static async Task<DbContextOptions<TAGDBContext>> SeedAsync(string databaseName)
    {
        var options = CreateOptions(databaseName);
        await VenueTestSupport.SeedAsync(options, new Venue { VenueID = 1, Name = "Terminal West", AddressID = AddressId, IsPublished = true }, OwnerId, StaffId);

        await using var seed = new TAGDBContext(options);
        seed.Users.Add(new User { UserID = OtherUserId, EmailOne = "other@example.com", Username = "other" });
        seed.Set<Venue>().AddRange(
            new Venue { VenueID = 2, Name = "The Earl", AddressID = AddressId, IsPublished = true },
            new Venue { VenueID = 3, Name = "Draft Hall", AddressID = AddressId, IsPublished = false });
        seed.Set<VenueTag>().AddRange(
            new VenueTag { VenueTagID = 1, Name = "parking" },
            new VenueTag { VenueTagID = 2, Name = "stage" },
            new VenueTag { VenueTagID = 3, Name = "wheelchair accessible" },
            new VenueTag { VenueTagID = 4, Name = "parking lot" },
            new VenueTag { VenueTagID = 5, Name = "orphan" });
        seed.Set<Linker_VenueToTag>().AddRange(
            Link(1, 1), Link(1, 2), Link(1, 3), Link(2, 1), Link(2, 2), Link(2, 4), Link(3, 1));
        await seed.SaveChangesAsync();
        return options;
    }

    private static Linker_VenueToTag Link(int venueId, int tagId) => new() { VenueID = venueId, VenueTagID = tagId };

    private static VenueTagController CreateTags(TAGDBContext context, int? userId)
    {
        var identity = userId.HasValue
            ? new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) }, "test")
            : new ClaimsIdentity();
        return new VenueTagController(context)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } },
        };
    }

    private static T Value<T>(ActionResult<T> result) =>
        Assert.IsAssignableFrom<T>(Assert.IsAssignableFrom<ObjectResult>(result.Result).Value);

    private static void AssertStatus(int expected, IActionResult? result) =>
        Assert.Equal(expected, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode ?? 0);

    private static string MessageOf(IActionResult? result) =>
        (string)Prop(Assert.IsAssignableFrom<ObjectResult>(result).Value, "message")!;

    private static List<string> TagNamesOf(TAGDBContext context, int venueId) =>
        context.Set<Linker_VenueToTag>().Where(l => l.VenueID == venueId).Select(l => l.Tag!.Name).OrderBy(n => n).ToList();

    // ---- normalization

    [Theory]
    [InlineData("parking", "parking")]
    [InlineData("  Parking ", "parking")]
    [InlineData("PARKING", "parking")]
    [InlineData("Wheelchair    Accessible", "wheelchair accessible")]
    [InlineData("wheelchair\taccessible", "wheelchair accessible")]
    [InlineData("Café", "café")]
    [InlineData("Cafe\u0301", "café")]
    [InlineData("24-hour", "24-hour")]
    [InlineData("Rock 'n' Roll", "rock 'n' roll")]
    [InlineData("Bar & Grill", "bar & grill")]
    [InlineData("12345678901234567890123456789a", "12345678901234567890123456789a")]
    public void Normalize_TrimsLowercasesAndCollapsesSpaces(string raw, string expected)
    {
        Assert.True(VenueTag.TryNormalize(raw, out var name, out var error));
        Assert.Equal(expected, name);
        Assert.Null(error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<b>bold</b>")]
    [InlineData("a<b")]
    [InlineData("a>b")]
    [InlineData("javascript:alert(1)")]
    [InlineData("say \"hi\"")]
    [InlineData("semi;colon")]
    [InlineData("under_score")]
    [InlineData("-leading hyphen")]
    [InlineData("'leading apostrophe")]
    [InlineData("party 🎉")]
    [InlineData("zero\u200Bwidth")]
    [InlineData("1234567890123456789012345678901")]
    public void Normalize_RejectsEmptyTooLongAndDisallowedCharacters(string? raw)
    {
        Assert.False(VenueTag.TryNormalize(raw, out var name, out var error));
        Assert.Equal(string.Empty, name);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Normalize_ErrorMessagesAreShortAndNameTheProblem()
    {
        VenueTag.TryNormalize(new string('a', 500), out _, out var tooLong);
        Assert.Contains("too long", tooLong);
        Assert.True(tooLong!.Length < 200);

        VenueTag.TryNormalize("a<b", out _, out var badChars);
        Assert.Contains("aren't allowed", badChars);
    }

    // ---- suggestions

    [Fact]
    public async Task Suggest_Anonymous_ReturnsMostUsedTagsStartingWithThePrefix()
    {
        var options = await SeedAsync(nameof(Suggest_Anonymous_ReturnsMostUsedTagsStartingWithThePrefix));
        await using var context = new TAGDBContext(options);

        var result = Value(await CreateTags(context, null).SuggestTags("par")).ToList();

        Assert.Equal(new[] { "parking", "parking lot" }, result.Select(s => s.Name));
        Assert.Equal(new[] { 3, 1 }, result.Select(s => s.VenueCount));
    }

    [Theory]
    [InlineData("PAR")]
    [InlineData("  par  ")]
    public async Task Suggest_IgnoresCaseAndSurroundingSpaces(string query)
    {
        var options = await SeedAsync($"{nameof(Suggest_IgnoresCaseAndSurroundingSpaces)}-{query.Trim()}");
        await using var context = new TAGDBContext(options);

        Assert.Equal(2, Value(await CreateTags(context, null).SuggestTags(query)).Count());
    }

    [Fact]
    public async Task Suggest_WithNoPrefix_ListsTheMostUsedTagsAndSkipsUnusedOnes()
    {
        var options = await SeedAsync(nameof(Suggest_WithNoPrefix_ListsTheMostUsedTagsAndSkipsUnusedOnes));
        await using var context = new TAGDBContext(options);

        var names = Value(await CreateTags(context, null).SuggestTags(null)).Select(s => s.Name).ToList();

        Assert.Equal(new[] { "parking", "stage", "parking lot", "wheelchair accessible" }, names);
        Assert.DoesNotContain("orphan", names);
    }

    [Fact]
    public async Task Suggest_LimitIsClamped_AndAnOverlongPrefixFindsNothing()
    {
        var options = await SeedAsync(nameof(Suggest_LimitIsClamped_AndAnOverlongPrefixFindsNothing));
        await using var context = new TAGDBContext(options);
        var controller = CreateTags(context, null);

        Assert.Single(Value(await controller.SuggestTags(null, 0)));
        Assert.Equal(2, Value(await controller.SuggestTags(null, 2)).Count());
        Assert.Equal(4, Value(await controller.SuggestTags(null, 10000)).Count());
        Assert.Empty(Value(await controller.SuggestTags(new string('p', 200))));
    }

    // ---- setting a venue's tags

    [Fact]
    public async Task SetTags_ByOwner_NormalizesDeduplicatesAndReusesExistingTags()
    {
        var options = await SeedAsync(nameof(SetTags_ByOwner_NormalizesDeduplicatesAndReusesExistingTags));
        await using var context = new TAGDBContext(options);

        var result = Value(await CreateTags(context, OwnerId).SetVenueTags(1, new VenueTagsRequest { Tags = new() { "Parking", "  parking ", "STAGE", "Craft  Beer" } }));

        Assert.Equal(new[] { "craft beer", "parking", "stage" }, result);
        await using var check = new TAGDBContext(options);
        Assert.Equal(new[] { "craft beer", "parking", "stage" }, TagNamesOf(check, 1));
        Assert.Equal(1, await check.Set<VenueTag>().CountAsync(t => t.Name == "parking"));
        Assert.Equal(1, await check.Set<VenueTag>().CountAsync(t => t.Name == "craft beer"));
    }

    [Fact]
    public async Task SetTags_ByOwner_IsAllowedOnTheirOwnVenueAndRemovesTagsThatAreLeftOut()
    {
        var options = await SeedAsync(nameof(SetTags_ByOwner_IsAllowedOnTheirOwnVenueAndRemovesTagsThatAreLeftOut));
        await using var context = new TAGDBContext(options);

        await CreateTags(context, OwnerId).SetVenueTags(1, new VenueTagsRequest { Tags = new() { "stage", "bar" } });

        await using var check = new TAGDBContext(options);
        Assert.Equal(new[] { "bar", "stage" }, TagNamesOf(check, 1));
        var log = await check.Set<Log>().SingleAsync(l => l.ShortText == "Venue tags changed");
        Assert.Equal("venueId=1;actingUserId=10;added=bar;removed=parking,wheelchair accessible", log.LoggedData);
    }

    [Fact]
    public async Task SetTags_ByStaff_Works_AndAnEmptyListClearsTheTags()
    {
        var options = await SeedAsync(nameof(SetTags_ByStaff_Works_AndAnEmptyListClearsTheTags));
        await using var context = new TAGDBContext(options);

        Assert.Empty(Value(await CreateTags(context, StaffId).SetVenueTags(1, new VenueTagsRequest { Tags = new() })));

        Assert.Empty(TagNamesOf(context, 1));
    }

    [Fact]
    public async Task SetTags_WithNoChange_WritesNoAuditEntry()
    {
        var options = await SeedAsync(nameof(SetTags_WithNoChange_WritesNoAuditEntry));
        await using var context = new TAGDBContext(options);

        await CreateTags(context, OwnerId).SetVenueTags(1, new VenueTagsRequest { Tags = new() { "Wheelchair Accessible", "parking", "stage" } });

        Assert.Empty(context.Set<Log>().Where(l => l.ShortText == "Venue tags changed"));
    }

    [Fact]
    public async Task SetTags_ByAnotherUserOrAnonymous_IsRejected()
    {
        var options = await SeedAsync(nameof(SetTags_ByAnotherUserOrAnonymous_IsRejected));
        await using var context = new TAGDBContext(options);

        Assert.IsType<UnauthorizedResult>((await CreateTags(context, null).SetVenueTags(1, new VenueTagsRequest { Tags = new() { "bar" } })).Result);
        AssertStatus(403, (await CreateTags(context, OtherUserId).SetVenueTags(1, new VenueTagsRequest { Tags = new() { "bar" } })).Result);
        AssertStatus(403, (await CreateTags(context, OwnerId).SetVenueTags(2, new VenueTagsRequest { Tags = new() { "bar" } })).Result);
        Assert.Equal(new[] { "parking", "stage", "wheelchair accessible" }, TagNamesOf(context, 1));
        Assert.Empty(context.Set<VenueTag>().Where(t => t.Name == "bar"));
    }

    [Fact]
    public async Task SetTags_ForAnUnknownVenue_IsNotFound()
    {
        var options = await SeedAsync(nameof(SetTags_ForAnUnknownVenue_IsNotFound));
        await using var context = new TAGDBContext(options);

        Assert.IsType<NotFoundResult>((await CreateTags(context, StaffId).SetVenueTags(77, new VenueTagsRequest { Tags = new() { "bar" } })).Result);
    }

    [Theory]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("javascript:alert(1)")]
    [InlineData("1234567890123456789012345678901")]
    [InlineData("   ")]
    public async Task SetTags_WithAnInvalidTag_IsRejectedWithAMessageAndChangesNothing(string badTag)
    {
        var options = await SeedAsync($"{nameof(SetTags_WithAnInvalidTag_IsRejectedWithAMessageAndChangesNothing)}-{badTag.Trim().Length}-{badTag.GetHashCode()}");
        await using var context = new TAGDBContext(options);

        var result = (await CreateTags(context, OwnerId).SetVenueTags(1, new VenueTagsRequest { Tags = new() { "good tag", badTag } })).Result;

        AssertStatus(400, result);
        Assert.False(string.IsNullOrWhiteSpace(MessageOf(result)));
        Assert.Equal(new[] { "parking", "stage", "wheelchair accessible" }, TagNamesOf(context, 1));
        Assert.Empty(context.Set<VenueTag>().Where(t => t.Name == "good tag"));
    }

    [Fact]
    public async Task SetTags_MissingList_IsBadRequest()
    {
        var options = await SeedAsync(nameof(SetTags_MissingList_IsBadRequest));
        await using var context = new TAGDBContext(options);

        AssertStatus(400, (await CreateTags(context, OwnerId).SetVenueTags(1, new VenueTagsRequest { Tags = null })).Result);
        AssertStatus(400, (await CreateTags(context, OwnerId).SetVenueTags(1, null!)).Result);
    }

    [Fact]
    public async Task SetTags_EnforcesTheMaximumPerVenue()
    {
        var options = await SeedAsync(nameof(SetTags_EnforcesTheMaximumPerVenue));
        await using var context = new TAGDBContext(options);
        var controller = CreateTags(context, OwnerId);

        var atTheLimit = Enumerable.Range(1, VenueTag.MaxTagsPerVenue).Select(i => $"tag {i}").ToList<string?>();
        Assert.Equal(VenueTag.MaxTagsPerVenue, Value(await controller.SetVenueTags(1, new VenueTagsRequest { Tags = atTheLimit })).Count());

        var overTheLimit = Enumerable.Range(1, VenueTag.MaxTagsPerVenue + 1).Select(i => $"extra {i}").ToList<string?>();
        AssertStatus(400, (await controller.SetVenueTags(1, new VenueTagsRequest { Tags = overTheLimit })).Result);

        var duplicatesDoNotCount = Enumerable.Repeat("Same Tag", 50).ToList<string?>();
        Assert.Single(Value(await controller.SetVenueTags(1, new VenueTagsRequest { Tags = duplicatesDoNotCount })));

        var absurd = Enumerable.Range(1, 500).Select(i => $"x{i}").ToList<string?>();
        AssertStatus(400, (await controller.SetVenueTags(1, new VenueTagsRequest { Tags = absurd })).Result);
    }

    // ---- staff tools

    [Fact]
    public async Task AdminList_RequiresStaff_AndPagesWithCountsAndSearch()
    {
        var options = await SeedAsync(nameof(AdminList_RequiresStaff_AndPagesWithCountsAndSearch));
        await using var context = new TAGDBContext(options);

        Assert.IsType<UnauthorizedResult>((await CreateTags(context, null).GetAllTags()).Result);
        AssertStatus(403, (await CreateTags(context, OwnerId).GetAllTags()).Result);

        var staff = CreateTags(context, StaffId);
        var first = Value(await staff.GetAllTags(limit: 2));
        Assert.Equal(new[] { "parking", "stage" }, first.Items.Select(t => t.Name));
        Assert.Equal(new[] { 3, 2 }, first.Items.Select(t => t.VenueCount));
        Assert.Equal(2, first.NextAfterId);

        var rest = Value(await staff.GetAllTags(afterId: first.NextAfterId!.Value, limit: 10));
        Assert.Equal(new[] { "wheelchair accessible", "parking lot", "orphan" }, rest.Items.Select(t => t.Name));
        Assert.Equal(0, rest.Items.Single(t => t.Name == "orphan").VenueCount);
        Assert.Null(rest.NextAfterId);

        var found = Value(await staff.GetAllTags(search: " PARKING "));
        Assert.Equal(new[] { "parking", "parking lot" }, found.Items.Select(t => t.Name));
    }

    [Fact]
    public async Task Delete_ByStaff_TakesTheTagOffEveryVenue()
    {
        var options = await SeedAsync(nameof(Delete_ByStaff_TakesTheTagOffEveryVenue));
        await using (var context = new TAGDBContext(options))
        {
            Assert.IsType<NoContentResult>(await CreateTags(context, StaffId).DeleteTag(1));
        }

        await using var check = new TAGDBContext(options);
        Assert.Empty(check.Set<VenueTag>().Where(t => t.Name == "parking"));
        Assert.Empty(check.Set<Linker_VenueToTag>().Where(l => l.VenueTagID == 1));
        Assert.Equal(new[] { "stage", "wheelchair accessible" }, TagNamesOf(check, 1));
        Assert.Contains("tag=parking;venues=3", (await check.Set<Log>().SingleAsync(l => l.ShortText == "Venue tag removed")).LoggedData);
    }

    [Fact]
    public async Task Delete_ByNonStaffOrForAnUnknownTag_IsRejected()
    {
        var options = await SeedAsync(nameof(Delete_ByNonStaffOrForAnUnknownTag_IsRejected));
        await using var context = new TAGDBContext(options);

        Assert.IsType<UnauthorizedResult>(await CreateTags(context, null).DeleteTag(1));
        AssertStatus(403, await CreateTags(context, OwnerId).DeleteTag(1));
        Assert.IsType<NotFoundResult>(await CreateTags(context, StaffId).DeleteTag(404));
        Assert.Equal(5, await context.Set<VenueTag>().CountAsync());
    }

    [Fact]
    public async Task Merge_MovesVenuesToTheTargetWithoutDuplicatesAndRemovesTheSource()
    {
        var options = await SeedAsync(nameof(Merge_MovesVenuesToTheTargetWithoutDuplicatesAndRemovesTheSource));
        await using (var context = new TAGDBContext(options))
        {
            // "parking lot" is on venue 2, which already has "parking": that copy is dropped, not duplicated.
            var merged = Value(await CreateTags(context, StaffId).MergeTag(4, new VenueTagMergeRequest { TargetTagId = 1 }));
            Assert.Equal("parking", merged.Name);
            Assert.Equal(3, merged.VenueCount);

            // "wheelchair accessible" is on venue 1 only, so it moves to "orphan".
            var moved = Value(await CreateTags(context, StaffId).MergeTag(3, new VenueTagMergeRequest { TargetTagId = 5 }));
            Assert.Equal(1, moved.VenueCount);
        }

        await using var check = new TAGDBContext(options);
        Assert.Empty(check.Set<VenueTag>().Where(t => t.VenueTagID == 3 || t.VenueTagID == 4));
        Assert.Equal(new[] { "orphan", "parking", "stage" }, TagNamesOf(check, 1));
        Assert.Equal(new[] { "parking", "stage" }, TagNamesOf(check, 2));
        Assert.Equal(2, await check.Set<Log>().CountAsync(l => l.ShortText == "Venue tags merged"));
    }

    [Fact]
    public async Task Merge_RejectsNonStaffSelfMergeAndUnknownTags()
    {
        var options = await SeedAsync(nameof(Merge_RejectsNonStaffSelfMergeAndUnknownTags));
        await using var context = new TAGDBContext(options);

        AssertStatus(403, (await CreateTags(context, OwnerId).MergeTag(1, new VenueTagMergeRequest { TargetTagId = 2 })).Result);
        AssertStatus(400, (await CreateTags(context, StaffId).MergeTag(1, new VenueTagMergeRequest { TargetTagId = 1 })).Result);
        AssertStatus(400, (await CreateTags(context, StaffId).MergeTag(1, new VenueTagMergeRequest { TargetTagId = 0 })).Result);
        Assert.IsType<NotFoundResult>((await CreateTags(context, StaffId).MergeTag(1, new VenueTagMergeRequest { TargetTagId = 404 })).Result);
        Assert.IsType<NotFoundResult>((await CreateTags(context, StaffId).MergeTag(404, new VenueTagMergeRequest { TargetTagId = 1 })).Result);
        Assert.Equal(5, await context.Set<VenueTag>().CountAsync());
    }

    // ---- venue API

    private static List<string> TagsIn(object? venue) =>
        ((System.Collections.IEnumerable)Prop(venue, "Tags")!).Cast<string>().ToList();

    private static IEnumerable<object> Items(ActionResult<IEnumerable<Venue>> result) =>
        ((System.Collections.IEnumerable)Assert.IsType<OkObjectResult>(result.Result).Value!).Cast<object>();

    [Fact]
    public async Task VenueApi_ReturnsTagsSortedOnEveryVenueResponse()
    {
        var options = await SeedAsync(nameof(VenueApi_ReturnsTagsSortedOnEveryVenueResponse));
        await using var context = new TAGDBContext(options);
        var venues = CreateController(context, null);
        var expected = new[] { "parking", "stage", "wheelchair accessible" };

        Assert.Equal(expected, TagsIn(Assert.IsType<OkObjectResult>((await venues.GetVenue(1)).Result).Value));
        Assert.Equal(expected, TagsIn(Assert.IsType<OkObjectResult>((await venues.GetVenueBySlug("terminal-west")).Result).Value));
        Assert.Equal(expected, TagsIn(Items(await venues.GetVenues()).Single(v => (int)Prop(v, "VenueID")! == 1)));
    }

    [Fact]
    public async Task VenueList_FilteredByTag_ReturnsOnlyPublishedVenuesThatHaveEveryTag()
    {
        var options = await SeedAsync(nameof(VenueList_FilteredByTag_ReturnsOnlyPublishedVenuesThatHaveEveryTag));
        await using var context = new TAGDBContext(options);
        var venues = CreateController(context, null);

        static List<int> Ids(IEnumerable<object> items) => items.Select(v => (int)Prop(v, "VenueID")!).OrderBy(i => i).ToList();

        Assert.Equal(new[] { 1, 2 }, Ids(Items(await venues.GetVenues(new[] { "parking" }))));
        Assert.Equal(new[] { 1, 2 }, Ids(Items(await venues.GetVenues(new[] { "Parking", "STAGE" }))));
        Assert.Equal(new[] { 1 }, Ids(Items(await venues.GetVenues(new[] { "  Wheelchair   Accessible " }))));
        Assert.Equal(new[] { 1 }, Ids(Items(await venues.GetVenues(new[] { "stage", "wheelchair accessible" }))));
        Assert.Equal(new[] { 1, 2 }, Ids(Items(await venues.GetVenues())));
    }

    [Fact]
    public async Task VenueList_FilteredByATagNoVenueHas_OrAnInvalidTag_IsEmpty()
    {
        var options = await SeedAsync(nameof(VenueList_FilteredByATagNoVenueHas_OrAnInvalidTag_IsEmpty));
        await using var context = new TAGDBContext(options);
        var venues = CreateController(context, null);

        Assert.Empty(Items(await venues.GetVenues(new[] { "orphan" })));
        Assert.Empty(Items(await venues.GetVenues(new[] { "stage", "no such tag" })));
        Assert.Empty(Items(await venues.GetVenues(new[] { "<script>alert(1)</script>" })));
        Assert.Empty(Items(await venues.GetVenues(new[] { "" })));
    }
}
