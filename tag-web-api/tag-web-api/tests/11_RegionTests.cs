using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TAGWEBAPI.Controllers;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;
using Xunit;
using static tagApiUnitTests.VenueTestSupport;

namespace tagApiUnitTests;

// Story #327: shared, staff-managed regions (statewide, multistate or within a state), linked to venues and artists, never deleted.
public class RegionTests
{
    private const int StaffId = 99;
    private const int OwnerId = 10;
    private const int OtherUserId = 11;

    // Regions: 1 Georgia (statewide GA), 2 Atlanta Metro (within GA), 3 Southeast (multistate AL, GA, TN), 4 Old Area (within AL, inactive).
    // Venue 1 is owned by OwnerId and is in Georgia. Artists 7 and 8 are also in Georgia.
    private static async Task<DbContextOptions<TAGDBContext>> SeedAsync(string databaseName)
    {
        var options = CreateOptions($"regions-{databaseName}");
        await VenueTestSupport.SeedAsync(options, new Venue { VenueID = 1, Name = "Terminal West", AddressID = AddressId, IsPublished = true }, OwnerId, StaffId);

        await using var seed = new TAGDBContext(options);
        seed.Users.Add(new User { UserID = OtherUserId, EmailOne = "other@example.com", Username = "other" });
        seed.Set<Region>().AddRange(
            new Region { RegionID = 1, Name = "Georgia", Slug = "georgia", Type = RegionTypes.Statewide, States = new() { "GA" }, IsActive = true },
            new Region { RegionID = 2, Name = "Atlanta Metro", Slug = "atlanta-metro", Type = RegionTypes.WithinState, States = new() { "GA" }, IsActive = true },
            new Region { RegionID = 3, Name = "Southeast", Slug = "southeast", Type = RegionTypes.Multistate, States = new() { "AL", "GA", "TN" }, IsActive = true },
            new Region { RegionID = 4, Name = "Old Area", Slug = "old-area", Type = RegionTypes.WithinState, States = new() { "AL" }, IsActive = false });
        seed.Set<Linker_VenueToRegion>().Add(new Linker_VenueToRegion { VenueID = 1, RegionID = 1 });
        seed.Set<Linker_ArtistToRegion>().AddRange(
            new Linker_ArtistToRegion { ArtistID = 7, RegionID = 1 },
            new Linker_ArtistToRegion { ArtistID = 8, RegionID = 1 });
        await seed.SaveChangesAsync();
        return options;
    }

    private static RegionController CreateRegions(TAGDBContext context, int? userId)
    {
        var identity = userId.HasValue
            ? new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) }, "test")
            : new ClaimsIdentity();
        return new RegionController(context)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } },
        };
    }

    private static T Value<T>(ActionResult<T> result) =>
        Assert.IsAssignableFrom<T>(Assert.IsAssignableFrom<ObjectResult>(result.Result).Value);

    private static void AssertStatus(int expected, IActionResult? result) =>
        Assert.Equal(expected, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode ?? 0);

    private static List<string> Names(IEnumerable<RegionDto> regions) => regions.Select(r => r.Name).ToList();

    private static RegionRequest Request(string? name, string? type, params string?[] states) =>
        new() { Name = name, Type = type, States = states.ToList() };

    // ---- type and states rules

    [Theory]
    [InlineData("Statewide", new[] { "GA" }, "Statewide", new[] { "GA" })]
    [InlineData("statewide", new[] { " ga " }, "Statewide", new[] { "GA" })]
    [InlineData("WITHINSTATE", new[] { "ga" }, "WithinState", new[] { "GA" })]
    [InlineData("withinstate", new[] { "DC" }, "WithinState", new[] { "DC" })]
    [InlineData("Multistate", new[] { "tn", "GA", "al", "ga" }, "Multistate", new[] { "AL", "GA", "TN" })]
    public void Normalize_AcceptsValidTypesAndStatesInCanonicalForm(string type, string[] states, string expectedType, string[] expectedStates)
    {
        Assert.True(Region.TryNormalize(type, states, out var normalizedType, out var normalizedStates, out var error));
        Assert.Equal(expectedType, normalizedType);
        Assert.Equal(expectedStates, normalizedStates);
        Assert.Null(error);
    }

    [Theory]
    [InlineData(null, new[] { "GA" })]
    [InlineData("", new[] { "GA" })]
    [InlineData("Province", new[] { "GA" })]
    [InlineData("Statewide", new string[0])]
    [InlineData("Statewide", new[] { "GA", "AL" })]
    [InlineData("WithinState", new string[0])]
    [InlineData("WithinState", new[] { "GA", "AL" })]
    [InlineData("Multistate", new string[0])]
    [InlineData("Multistate", new[] { "GA" })]
    [InlineData("Multistate", new[] { "GA", "ga" })]
    [InlineData("Statewide", new[] { "XX" })]
    [InlineData("Statewide", new[] { "Georgia" })]
    [InlineData("Statewide", new[] { "" })]
    [InlineData("Multistate", new[] { "GA", "ZZ" })]
    public void Normalize_RejectsAWrongTypeStateCodeOrStateCount(string? type, string[] states)
    {
        Assert.False(Region.TryNormalize(type, states, out _, out var normalizedStates, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.True(normalizedStates.Count <= UsStates.Names.Count);
    }

    [Fact]
    public void Normalize_NullStateEntryIsRejected()
    {
        Assert.False(Region.TryNormalize("Statewide", new string?[] { null }, out _, out _, out var error));
        Assert.Contains("state code", error);
    }

    [Fact]
    public void UsStates_HasTheFiftyStatesAndDc()
    {
        Assert.Equal(51, UsStates.Names.Count);
        Assert.All(UsStates.Names.Keys, code => Assert.Equal(2, code.Length));
    }

    // ---- public list

    [Fact]
    public async Task List_Anonymous_ReturnsOnlyActiveRegionsByName()
    {
        var options = await SeedAsync(nameof(List_Anonymous_ReturnsOnlyActiveRegionsByName));
        await using var context = new TAGDBContext(options);

        var result = Value(await CreateRegions(context, null).GetActiveRegions());

        Assert.Equal(new[] { "Atlanta Metro", "Georgia", "Southeast" }, Names(result));
        Assert.Equal(new[] { "AL", "GA", "TN" }, result.Single(r => r.Name == "Southeast").States);
    }

    [Theory]
    [InlineData("GA", new[] { "Atlanta Metro", "Georgia", "Southeast" })]
    [InlineData("ga", new[] { "Atlanta Metro", "Georgia", "Southeast" })]
    [InlineData(" tn ", new[] { "Southeast" })]
    [InlineData("AL", new[] { "Southeast" })]
    [InlineData("FL", new string[0])]
    [InlineData("XX", new string[0])]
    public async Task List_FilteredByState_ReturnsActiveRegionsThatCoverIt(string state, string[] expected)
    {
        var options = await SeedAsync($"{nameof(List_FilteredByState_ReturnsActiveRegionsThatCoverIt)}-{state.Trim()}");
        await using var context = new TAGDBContext(options);

        Assert.Equal(expected, Names(Value(await CreateRegions(context, null).GetActiveRegions(state))));
    }

    // ---- staff management

    [Fact]
    public async Task AdminList_RequiresStaff_AndShowsInactiveRegionsWithVenueAndArtistCounts()
    {
        var options = await SeedAsync(nameof(AdminList_RequiresStaff_AndShowsInactiveRegionsWithVenueAndArtistCounts));
        await using var context = new TAGDBContext(options);

        Assert.IsType<UnauthorizedResult>((await CreateRegions(context, null).GetAllRegions()).Result);
        AssertStatus(403, (await CreateRegions(context, OwnerId).GetAllRegions()).Result);

        var all = Value(await CreateRegions(context, StaffId).GetAllRegions()).ToList();
        Assert.Equal(4, all.Count);
        var georgia = all.Single(r => r.Slug == "georgia");
        Assert.Equal(1, georgia.VenueCount);
        Assert.Equal(2, georgia.ArtistCount);
        Assert.False(all.Single(r => r.Slug == "old-area").IsActive);
    }

    [Fact]
    public async Task Create_NonStaffAndAnonymous_AreRejected()
    {
        var options = await SeedAsync(nameof(Create_NonStaffAndAnonymous_AreRejected));
        await using var context = new TAGDBContext(options);

        Assert.IsType<UnauthorizedResult>((await CreateRegions(context, null).CreateRegion(Request("Texas", "Statewide", "TX"))).Result);
        AssertStatus(403, (await CreateRegions(context, OwnerId).CreateRegion(Request("Texas", "Statewide", "TX"))).Result);
        Assert.Equal(4, await context.Set<Region>().CountAsync());
    }

    [Theory]
    [InlineData("Texas", "Statewide", new[] { "TX" }, "texas", "Statewide")]
    [InlineData("Austin Area", "WithinState", new[] { "tx" }, "austin-area", "WithinState")]
    [InlineData("  Gulf Coast ", "multistate", new[] { "TX", "LA", "MS", "AL", "FL" }, "gulf-coast", "Multistate")]
    public async Task Create_ByStaff_AcceptsStatewideMultistateAndWithinStateRegions(string name, string type, string[] states, string expectedSlug, string expectedType)
    {
        var options = await SeedAsync($"{nameof(Create_ByStaff_AcceptsStatewideMultistateAndWithinStateRegions)}-{expectedSlug}");
        await using var context = new TAGDBContext(options);

        var created = Value(await CreateRegions(context, StaffId).CreateRegion(Request(name, type, states)));

        Assert.Equal(name.Trim(), created.Name);
        Assert.Equal(expectedSlug, created.Slug);
        Assert.Equal(expectedType, created.Type);
        Assert.True(created.IsActive);
        Assert.Equal(states.Select(s => s.ToUpperInvariant()).OrderBy(s => s, StringComparer.Ordinal), created.States);
        Assert.Equal(0, created.VenueCount);

        await using var check = new TAGDBContext(options);
        var log = await check.Set<Log>().SingleAsync(l => l.ShortText == "Region created");
        Assert.Equal(StaffId, log.UserID);
        Assert.Contains($"slug={expectedSlug}", log.LoggedData);
    }

    [Theory]
    [InlineData("georgia")]
    [InlineData("GEORGIA")]
    [InlineData("  Georgia  ")]
    [InlineData("Atlanta-Metro")]
    public async Task Create_DuplicateNameOrSlug_IsConflict(string name)
    {
        var options = await SeedAsync($"{nameof(Create_DuplicateNameOrSlug_IsConflict)}-{name.Trim()}");
        await using var context = new TAGDBContext(options);

        AssertStatus(409, (await CreateRegions(context, StaffId).CreateRegion(Request(name, "Statewide", "GA"))).Result);
        Assert.Equal(4, await context.Set<Region>().CountAsync());
    }

    [Theory]
    [InlineData(null, "Statewide", "GA")]
    [InlineData("   ", "Statewide", "GA")]
    [InlineData("<b>Bold</b>", "Statewide", "GA")]
    [InlineData("!!!", "Statewide", "GA")]
    [InlineData("Valid Name", "Statewide", "GA,AL")]
    [InlineData("Valid Name", "Multistate", "GA")]
    [InlineData("Valid Name", "Planet", "GA")]
    [InlineData("Valid Name", "Statewide", "XX")]
    public async Task Create_InvalidNameTypeOrStates_IsBadRequest(string? name, string type, string statesCsv)
    {
        var options = await SeedAsync($"{nameof(Create_InvalidNameTypeOrStates_IsBadRequest)}-{name}-{type}-{statesCsv}");
        await using var context = new TAGDBContext(options);

        var result = await CreateRegions(context, StaffId).CreateRegion(Request(name, type, statesCsv.Split(',')));

        AssertStatus(400, result.Result);
        Assert.Equal(4, await context.Set<Region>().CountAsync());
    }

    [Fact]
    public async Task Create_NameOverTheLimit_IsBadRequest()
    {
        var options = await SeedAsync(nameof(Create_NameOverTheLimit_IsBadRequest));
        await using var context = new TAGDBContext(options);

        var result = await CreateRegions(context, StaffId).CreateRegion(Request(new string('a', Region.NameMaxLength + 1), "Statewide", "TX"));

        AssertStatus(400, result.Result);
    }

    [Fact]
    public async Task Update_ByStaff_ChangesNameTypeAndStatesButKeepsTheSlug()
    {
        var options = await SeedAsync(nameof(Update_ByStaff_ChangesNameTypeAndStatesButKeepsTheSlug));
        await using var context = new TAGDBContext(options);

        var updated = Value(await CreateRegions(context, StaffId).UpdateRegion(2, Request("Greater Atlanta", "multistate", "GA", "AL")));

        Assert.Equal("Greater Atlanta", updated.Name);
        Assert.Equal("atlanta-metro", updated.Slug);
        Assert.Equal("Multistate", updated.Type);
        Assert.Equal(new[] { "AL", "GA" }, updated.States);

        await using var check = new TAGDBContext(options);
        Assert.Contains("Region updated", check.Set<Log>().Select(l => l.ShortText));
    }

    [Fact]
    public async Task Update_KeepingTheSameNameInAnotherCase_IsAllowed_ButAnotherRegionsNameIsNot()
    {
        var options = await SeedAsync(nameof(Update_KeepingTheSameNameInAnotherCase_IsAllowed_ButAnotherRegionsNameIsNot));
        await using var context = new TAGDBContext(options);
        var controller = CreateRegions(context, StaffId);

        Assert.Equal("GEORGIA", Value(await controller.UpdateRegion(1, Request("GEORGIA", "Statewide", "GA"))).Name);
        AssertStatus(409, (await controller.UpdateRegion(1, Request("southeast", "Statewide", "GA"))).Result);
    }

    [Fact]
    public async Task Update_ByNonStaffUnknownRegionOrInvalidInput_IsRejected()
    {
        var options = await SeedAsync(nameof(Update_ByNonStaffUnknownRegionOrInvalidInput_IsRejected));
        await using var context = new TAGDBContext(options);

        AssertStatus(403, (await CreateRegions(context, OwnerId).UpdateRegion(1, Request("Hijacked", "Statewide", "GA"))).Result);
        Assert.IsType<NotFoundResult>((await CreateRegions(context, StaffId).UpdateRegion(77, Request("Nope", "Statewide", "GA"))).Result);
        AssertStatus(400, (await CreateRegions(context, StaffId).UpdateRegion(1, Request("Georgia", "Statewide", "GA", "AL"))).Result);
        Assert.Equal("Georgia", (await context.Set<Region>().FindAsync(1))!.Name);
    }

    [Fact]
    public async Task Deactivate_HidesTheRegionFromThePublicListButVenuesKeepIt()
    {
        var options = await SeedAsync(nameof(Deactivate_HidesTheRegionFromThePublicListButVenuesKeepIt));
        await using (var context = new TAGDBContext(options))
        {
            var result = Value(await CreateRegions(context, StaffId).SetRegionActive(1, new RegionActiveRequest { IsActive = false }));
            Assert.False(result.IsActive);
            Assert.Equal(1, result.VenueCount);
            Assert.Equal(2, result.ArtistCount);
        }

        await using var check = new TAGDBContext(options);
        Assert.DoesNotContain("Georgia", Names(Value(await CreateRegions(check, null).GetActiveRegions())));

        var venue = Assert.IsType<OkObjectResult>((await CreateController(check, null).GetVenue(1)).Result).Value;
        var regions = ((System.Collections.IEnumerable)Prop(venue, "Regions")!).Cast<object>().ToList();
        var georgia = Assert.Single(regions);
        Assert.Equal("Georgia", Prop(georgia, "Name"));
        Assert.Equal(false, Prop(georgia, "IsActive"));
        Assert.Equal(2, await check.Set<Linker_ArtistToRegion>().CountAsync(l => l.RegionID == 1));
    }

    [Fact]
    public async Task Reactivate_BringsTheRegionBackAndRepeatedCallsWriteNoExtraAuditEntries()
    {
        var options = await SeedAsync(nameof(Reactivate_BringsTheRegionBackAndRepeatedCallsWriteNoExtraAuditEntries));
        await using var context = new TAGDBContext(options);
        var controller = CreateRegions(context, StaffId);

        await controller.SetRegionActive(4, new RegionActiveRequest { IsActive = true });
        await controller.SetRegionActive(4, new RegionActiveRequest { IsActive = true });

        Assert.Contains("Old Area", Names(Value(await controller.GetActiveRegions())));
        Assert.Single(context.Set<Log>().Where(l => l.ShortText == "Region activated"));
    }

    [Fact]
    public async Task SetActive_ByNonStaffOrForAnUnknownRegion_IsRejected()
    {
        var options = await SeedAsync(nameof(SetActive_ByNonStaffOrForAnUnknownRegion_IsRejected));
        await using var context = new TAGDBContext(options);

        AssertStatus(403, (await CreateRegions(context, OwnerId).SetRegionActive(1, new RegionActiveRequest { IsActive = false })).Result);
        Assert.IsType<NotFoundResult>((await CreateRegions(context, StaffId).SetRegionActive(77, new RegionActiveRequest { IsActive = false })).Result);
        Assert.True((await context.Set<Region>().FindAsync(1))!.IsActive);
    }

    // ---- venue regions

    [Fact]
    public async Task SetVenueRegions_ByOwner_ReplacesTheRegionsAndAllowsSeveral()
    {
        var options = await SeedAsync(nameof(SetVenueRegions_ByOwner_ReplacesTheRegionsAndAllowsSeveral));
        await using var context = new TAGDBContext(options);

        var result = Value(await CreateRegions(context, OwnerId).SetVenueRegions(1, new VenueRegionsRequest { RegionIds = new() { 2, 3, 3 } }));

        Assert.Equal(new[] { "Atlanta Metro", "Southeast" }, Names(result));
        await using var check = new TAGDBContext(options);
        Assert.Equal(new[] { 2, 3 }, check.Set<Linker_VenueToRegion>().Where(l => l.VenueID == 1).Select(l => l.RegionID).OrderBy(i => i).ToArray());
        var log = await check.Set<Log>().SingleAsync(l => l.ShortText == "Venue regions changed");
        Assert.Equal("venueId=1;actingUserId=10;added=2,3;removed=1", log.LoggedData);
    }

    [Fact]
    public async Task SetVenueRegions_ByStaff_Works_AndAnEmptyListClearsThem()
    {
        var options = await SeedAsync(nameof(SetVenueRegions_ByStaff_Works_AndAnEmptyListClearsThem));
        await using var context = new TAGDBContext(options);

        Assert.Equal(new[] { "Georgia", "Southeast" }, Names(Value(await CreateRegions(context, StaffId).SetVenueRegions(1, new VenueRegionsRequest { RegionIds = new() { 1, 3 } }))));
        Assert.Empty(Value(await CreateRegions(context, StaffId).SetVenueRegions(1, new VenueRegionsRequest { RegionIds = new() })));
        Assert.Empty(context.Set<Linker_VenueToRegion>());
    }

    [Fact]
    public async Task SetVenueRegions_ByAnotherUserOrAnonymous_IsRejected()
    {
        var options = await SeedAsync(nameof(SetVenueRegions_ByAnotherUserOrAnonymous_IsRejected));
        await using var context = new TAGDBContext(options);

        Assert.IsType<UnauthorizedResult>((await CreateRegions(context, null).SetVenueRegions(1, new VenueRegionsRequest { RegionIds = new() { 2 } })).Result);
        AssertStatus(403, (await CreateRegions(context, OtherUserId).SetVenueRegions(1, new VenueRegionsRequest { RegionIds = new() { 2 } })).Result);
        Assert.Equal(new[] { 1 }, context.Set<Linker_VenueToRegion>().Select(l => l.RegionID).ToArray());
    }

    [Fact]
    public async Task SetVenueRegions_ForAnUnknownVenue_IsNotFound()
    {
        var options = await SeedAsync(nameof(SetVenueRegions_ForAnUnknownVenue_IsNotFound));
        await using var context = new TAGDBContext(options);

        Assert.IsType<NotFoundResult>((await CreateRegions(context, StaffId).SetVenueRegions(55, new VenueRegionsRequest { RegionIds = new() { 1 } })).Result);
    }

    [Fact]
    public async Task SetVenueRegions_RejectsMissingBadAndUnknownIds()
    {
        var options = await SeedAsync(nameof(SetVenueRegions_RejectsMissingBadAndUnknownIds));
        await using var context = new TAGDBContext(options);
        var controller = CreateRegions(context, OwnerId);

        AssertStatus(400, (await controller.SetVenueRegions(1, new VenueRegionsRequest { RegionIds = null })).Result);
        AssertStatus(400, (await controller.SetVenueRegions(1, new VenueRegionsRequest { RegionIds = new() { 0 } })).Result);
        AssertStatus(400, (await controller.SetVenueRegions(1, new VenueRegionsRequest { RegionIds = new() { 2, 404 } })).Result);
        Assert.Equal(new[] { 1 }, context.Set<Linker_VenueToRegion>().Select(l => l.RegionID).ToArray());
    }

    [Fact]
    public async Task SetVenueRegions_CannotAddAnInactiveRegion_ButCanKeepOneItAlreadyHas()
    {
        var options = await SeedAsync(nameof(SetVenueRegions_CannotAddAnInactiveRegion_ButCanKeepOneItAlreadyHas));
        await using (var context = new TAGDBContext(options))
        {
            AssertStatus(400, (await CreateRegions(context, OwnerId).SetVenueRegions(1, new VenueRegionsRequest { RegionIds = new() { 1, 4 } })).Result);
        }

        await using (var seed = new TAGDBContext(options))
        {
            (await seed.Set<Region>().FindAsync(1))!.IsActive = false;
            await seed.SaveChangesAsync();
        }

        await using var check = new TAGDBContext(options);
        var kept = Value(await CreateRegions(check, OwnerId).SetVenueRegions(1, new VenueRegionsRequest { RegionIds = new() { 1, 2 } }));
        Assert.Equal(new[] { "Atlanta Metro", "Georgia" }, Names(kept));
    }

    [Fact]
    public async Task SetVenueRegions_WithNoChange_WritesNoAuditEntry()
    {
        var options = await SeedAsync(nameof(SetVenueRegions_WithNoChange_WritesNoAuditEntry));
        await using var context = new TAGDBContext(options);

        await CreateRegions(context, OwnerId).SetVenueRegions(1, new VenueRegionsRequest { RegionIds = new() { 1 } });

        Assert.Empty(context.Set<Log>().Where(l => l.ShortText == "Venue regions changed"));
    }

    // ---- venue API

    [Fact]
    public async Task VenueApi_ReturnsRegionsOnTheSingleVenueTheListAndBySlug()
    {
        var options = await SeedAsync(nameof(VenueApi_ReturnsRegionsOnTheSingleVenueTheListAndBySlug));
        await using (var seed = new TAGDBContext(options))
        {
            seed.Set<Linker_VenueToRegion>().Add(new Linker_VenueToRegion { VenueID = 1, RegionID = 3 });
            await seed.SaveChangesAsync();
        }

        await using var context = new TAGDBContext(options);
        var venues = CreateController(context, null);

        static List<string?> Slugs(object? venue) =>
            ((System.Collections.IEnumerable)Prop(venue, "Regions")!).Cast<object>().Select(r => (string?)Prop(r, "Slug")).ToList();

        var single = Assert.IsType<OkObjectResult>((await venues.GetVenue(1)).Result).Value;
        var bySlug = Assert.IsType<OkObjectResult>((await venues.GetVenueBySlug("terminal-west")).Result).Value;
        var list = ((System.Collections.IEnumerable)Assert.IsType<OkObjectResult>((await venues.GetVenues()).Result).Value!).Cast<object>().Single();

        Assert.Equal(new[] { "georgia", "southeast" }, Slugs(single));
        Assert.Equal(new[] { "georgia", "southeast" }, Slugs(bySlug));
        Assert.Equal(new[] { "georgia", "southeast" }, Slugs(list));

        var southeast = ((System.Collections.IEnumerable)Prop(single, "Regions")!).Cast<object>().Last();
        Assert.Equal("Multistate", Prop(southeast, "Type"));
        Assert.Equal(new[] { "AL", "GA", "TN" }, (IEnumerable<string>)Prop(southeast, "States")!);
    }

    // ---- staff import region hints

    private static VenueImportController CreateImporter(TAGDBContext context)
    {
        return new VenueImportController(context, NullLogger<VenueImportController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, StaffId.ToString()) }, "test")) },
            },
        };
    }

    private static VenueImportRequest Hinted(string name, string street, string? regionHint, string? categoryHint = null) => new()
    {
        Venues = new() { new VenueImportItem { Name = name, AddressLine1 = street, City = "Atlanta", State = "GA", RegionHint = regionHint, CategoryHint = categoryHint } },
    };

    [Theory]
    [InlineData("Atlanta Metro")]
    [InlineData("atlanta metro")]
    [InlineData("ATLANTA-METRO")]
    public async Task Import_RegionHint_LinksANewVenueToTheMatchingActiveRegion_AndKeepsTheAddressText(string hint)
    {
        var options = await SeedAsync($"{nameof(Import_RegionHint_LinksANewVenueToTheMatchingActiveRegion_AndKeepsTheAddressText)}-{hint}");
        await using var context = new TAGDBContext(options);

        var response = Value(await CreateImporter(context).ImportVenues(Hinted("The Earl", "488 Flat Shoals Ave SE", hint)));

        var row = Assert.Single(response.Rows);
        Assert.Equal(VenueImportController.StatusCreated, row.Status);
        Assert.Null(row.Message);
        Assert.Equal(new[] { 2 }, context.Set<Linker_VenueToRegion>().Where(l => l.VenueID == row.VenueID).Select(l => l.RegionID).ToArray());
        Assert.Equal(hint, (await context.Set<Venue>().Include(v => v.Address).SingleAsync(v => v.VenueID == row.VenueID)).Address!.Region);
    }

    [Theory]
    [InlineData("Midtown")]
    [InlineData("Old Area")]
    public async Task Import_UnknownOrInactiveRegionHint_CreatesTheVenueWithoutARegionAndSaysSo(string hint)
    {
        var options = await SeedAsync($"{nameof(Import_UnknownOrInactiveRegionHint_CreatesTheVenueWithoutARegionAndSaysSo)}-{hint}");
        await using var context = new TAGDBContext(options);

        var response = Value(await CreateImporter(context).ImportVenues(Hinted("The Earl", "488 Flat Shoals Ave SE", hint)));

        var row = Assert.Single(response.Rows);
        Assert.Equal(VenueImportController.StatusCreated, row.Status);
        Assert.Contains("did not match a shared region", row.Message);
        Assert.Empty(context.Set<Linker_VenueToRegion>().Where(l => l.VenueID == row.VenueID));
    }

    [Fact]
    public async Task Import_BothHintsUnmatched_ReportsBothInTheMessage()
    {
        var options = await SeedAsync(nameof(Import_BothHintsUnmatched_ReportsBothInTheMessage));
        await using var context = new TAGDBContext(options);

        var response = Value(await CreateImporter(context).ImportVenues(Hinted("The Earl", "488 Flat Shoals Ave SE", "Midtown", "Dance Halls")));

        var message = Assert.Single(response.Rows).Message;
        Assert.Contains("Category hint 'Dance Halls'", message);
        Assert.Contains("Region hint 'Midtown'", message);
    }

    [Fact]
    public async Task Import_RegionHint_FillsInAnUnregionedUnownedVenueButNeverChangesExistingRegions()
    {
        var options = await SeedAsync(nameof(Import_RegionHint_FillsInAnUnregionedUnownedVenueButNeverChangesExistingRegions));
        await using (var seed = new TAGDBContext(options))
        {
            seed.Set<Address>().Add(new Address { AddressID = 901, AddressLine1 = "1 Pine St", City = "Atlanta", State = "GA", ZipCode = "30301", Country = "USA" });
            seed.Set<Venue>().Add(new Venue { VenueID = 5, Name = "Pine Hall", AddressID = 901, IsPublished = true });
            await seed.SaveChangesAsync();
        }

        await using var context = new TAGDBContext(options);

        var first = Value(await CreateImporter(context).ImportVenues(Hinted("Pine Hall", "1 Pine Street", "Atlanta Metro")));
        Assert.Equal(VenueImportController.StatusUpdated, first.Rows[0].Status);

        var second = Value(await CreateImporter(context).ImportVenues(Hinted("Pine Hall", "1 Pine Street", "Southeast")));
        Assert.Equal(new[] { 2 }, context.Set<Linker_VenueToRegion>().Where(l => l.VenueID == 5).Select(l => l.RegionID).ToArray());
        Assert.Equal(VenueImportController.StatusUpdated, second.Rows[0].Status); // the address text changed, the region did not
    }
}
