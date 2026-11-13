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

// Story #325: staff-managed venue categories, linked to venues, never deleted.
public class VenueCategoryTests
{
    private const int StaffId = 99;
    private const int OwnerId = 10;
    private const int OtherUserId = 11;

    // Categories: 1 Theaters (active), 2 Bars (active), 3 Old Spaces (inactive). Venue 1 is owned by OwnerId and is a Theater.
    private static async Task<DbContextOptions<TAGDBContext>> SeedAsync(string databaseName)
    {
        var options = CreateOptions(databaseName);
        await VenueTestSupport.SeedAsync(options, new Venue { VenueID = 1, Name = "Terminal West", AddressID = AddressId, IsPublished = true }, OwnerId, StaffId);

        await using var seed = new TAGDBContext(options);
        seed.Users.Add(new User { UserID = OtherUserId, EmailOne = "other@example.com", Username = "other" });
        seed.Set<VenueCategory>().AddRange(
            new VenueCategory { VenueCategoryID = 1, Name = "Theaters", Slug = "theaters", IsActive = true, SortOrder = 20 },
            new VenueCategory { VenueCategoryID = 2, Name = "Bars", Slug = "bars", IsActive = true, SortOrder = 10 },
            new VenueCategory { VenueCategoryID = 3, Name = "Old Spaces", Slug = "old-spaces", IsActive = false, SortOrder = 30 });
        seed.Set<Linker_VenueToCategory>().Add(new Linker_VenueToCategory { VenueID = 1, VenueCategoryID = 1 });
        await seed.SaveChangesAsync();
        return options;
    }

    private static VenueCategoryController CreateCategories(TAGDBContext context, int? userId)
    {
        var identity = userId.HasValue
            ? new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) }, "test")
            : new ClaimsIdentity();
        return new VenueCategoryController(context)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } },
        };
    }

    private static T Value<T>(ActionResult<T> result) =>
        Assert.IsAssignableFrom<T>(Assert.IsAssignableFrom<ObjectResult>(result.Result).Value);

    private static void AssertStatus(int expected, IActionResult? result) =>
        Assert.Equal(expected, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode ?? 0);

    private static List<string> Names(IEnumerable<VenueCategoryDto> categories) => categories.Select(c => c.Name).ToList();

    // ---- public list

    [Fact]
    public async Task List_Anonymous_ReturnsOnlyActiveCategoriesInDisplayOrder()
    {
        var options = await SeedAsync(nameof(List_Anonymous_ReturnsOnlyActiveCategoriesInDisplayOrder));
        await using var context = new TAGDBContext(options);

        var result = Value(await CreateCategories(context, null).GetActiveCategories());

        Assert.Equal(new[] { "Bars", "Theaters" }, Names(result));
    }

    // ---- staff management

    [Fact]
    public async Task AdminList_RequiresStaff_AndShowsInactiveCategoriesWithVenueCounts()
    {
        var options = await SeedAsync(nameof(AdminList_RequiresStaff_AndShowsInactiveCategoriesWithVenueCounts));
        await using var context = new TAGDBContext(options);

        Assert.IsType<UnauthorizedResult>((await CreateCategories(context, null).GetAllCategories()).Result);
        AssertStatus(403, (await CreateCategories(context, OwnerId).GetAllCategories()).Result);

        var all = Value(await CreateCategories(context, StaffId).GetAllCategories()).ToList();
        Assert.Equal(3, all.Count);
        Assert.Equal(1, all.Single(c => c.Slug == "theaters").VenueCount);
        Assert.False(all.Single(c => c.Slug == "old-spaces").IsActive);
    }

    [Fact]
    public async Task Create_NonStaffAndAnonymous_AreRejected()
    {
        var options = await SeedAsync(nameof(Create_NonStaffAndAnonymous_AreRejected));
        await using var context = new TAGDBContext(options);

        Assert.IsType<UnauthorizedResult>((await CreateCategories(context, null).CreateCategory(new VenueCategoryRequest { Name = "Rooftops" })).Result);
        AssertStatus(403, (await CreateCategories(context, OwnerId).CreateCategory(new VenueCategoryRequest { Name = "Rooftops" })).Result);
        Assert.Equal(3, await context.Set<VenueCategory>().CountAsync());
    }

    [Fact]
    public async Task Create_ByStaff_AddsAnActiveCategoryWithASlugAndAnAuditEntry()
    {
        var options = await SeedAsync(nameof(Create_ByStaff_AddsAnActiveCategoryWithASlugAndAnAuditEntry));
        await using var context = new TAGDBContext(options);

        var created = Value(await CreateCategories(context, StaffId).CreateCategory(new VenueCategoryRequest { Name = "  Roof Top Spaces " }));

        Assert.Equal("Roof Top Spaces", created.Name);
        Assert.Equal("roof-top-spaces", created.Slug);
        Assert.True(created.IsActive);
        Assert.Equal(40, created.SortOrder);

        await using var check = new TAGDBContext(options);
        var log = await check.Set<Log>().SingleAsync(l => l.ShortText == "Venue category created");
        Assert.Equal(StaffId, log.UserID);
        Assert.Contains("slug=roof-top-spaces", log.LoggedData);
    }

    [Theory]
    [InlineData("theaters")]
    [InlineData("THEATERS")]
    [InlineData("  Theaters  ")]
    [InlineData("Old-Spaces")]
    public async Task Create_DuplicateNameOrSlug_IsConflict(string name)
    {
        var options = await SeedAsync($"{nameof(Create_DuplicateNameOrSlug_IsConflict)}-{name.Trim()}");
        await using var context = new TAGDBContext(options);

        var result = await CreateCategories(context, StaffId).CreateCategory(new VenueCategoryRequest { Name = name });

        AssertStatus(409, result.Result);
        Assert.Equal(3, await context.Set<VenueCategory>().CountAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("<b>Bold</b>")]
    [InlineData("!!!")]
    public async Task Create_InvalidName_IsBadRequest(string? name)
    {
        var options = await SeedAsync($"{nameof(Create_InvalidName_IsBadRequest)}-{name}");
        await using var context = new TAGDBContext(options);

        var result = await CreateCategories(context, StaffId).CreateCategory(new VenueCategoryRequest { Name = name });

        AssertStatus(400, result.Result);
        Assert.Equal(3, await context.Set<VenueCategory>().CountAsync());
    }

    [Fact]
    public async Task Create_NameOverTheLimit_IsBadRequest()
    {
        var options = await SeedAsync(nameof(Create_NameOverTheLimit_IsBadRequest));
        await using var context = new TAGDBContext(options);

        var result = await CreateCategories(context, StaffId).CreateCategory(new VenueCategoryRequest { Name = new string('a', VenueCategory.NameMaxLength + 1) });

        AssertStatus(400, result.Result);
    }

    [Fact]
    public async Task Rename_ByStaff_ChangesTheNameButKeepsTheSlug()
    {
        var options = await SeedAsync(nameof(Rename_ByStaff_ChangesTheNameButKeepsTheSlug));
        await using var context = new TAGDBContext(options);

        var updated = Value(await CreateCategories(context, StaffId).UpdateCategory(1, new VenueCategoryRequest { Name = "Theatres", SortOrder = 5 }));

        Assert.Equal("Theatres", updated.Name);
        Assert.Equal("theaters", updated.Slug);
        Assert.Equal(5, updated.SortOrder);
        Assert.Equal(1, updated.VenueCount);
    }

    [Fact]
    public async Task Rename_ToAnExistingNameOrUnknownCategoryOrByNonStaff_IsRejected()
    {
        var options = await SeedAsync(nameof(Rename_ToAnExistingNameOrUnknownCategoryOrByNonStaff_IsRejected));
        await using var context = new TAGDBContext(options);

        AssertStatus(409, (await CreateCategories(context, StaffId).UpdateCategory(1, new VenueCategoryRequest { Name = "bars" })).Result);
        Assert.IsType<NotFoundResult>((await CreateCategories(context, StaffId).UpdateCategory(77, new VenueCategoryRequest { Name = "Nope" })).Result);
        AssertStatus(403, (await CreateCategories(context, OwnerId).UpdateCategory(1, new VenueCategoryRequest { Name = "Hijacked" })).Result);
        Assert.Equal("Theaters", (await context.Set<VenueCategory>().FindAsync(1))!.Name);
    }

    [Fact]
    public async Task Rename_KeepingTheSameNameInAnotherCase_IsAllowed()
    {
        var options = await SeedAsync(nameof(Rename_KeepingTheSameNameInAnotherCase_IsAllowed));
        await using var context = new TAGDBContext(options);

        var updated = Value(await CreateCategories(context, StaffId).UpdateCategory(1, new VenueCategoryRequest { Name = "THEATERS" }));

        Assert.Equal("THEATERS", updated.Name);
    }

    [Fact]
    public async Task Deactivate_HidesTheCategoryFromThePublicListButVenuesKeepIt()
    {
        var options = await SeedAsync(nameof(Deactivate_HidesTheCategoryFromThePublicListButVenuesKeepIt));
        await using (var context = new TAGDBContext(options))
        {
            var result = Value(await CreateCategories(context, StaffId).SetCategoryActive(1, new VenueCategoryActiveRequest { IsActive = false }));
            Assert.False(result.IsActive);
            Assert.Equal(1, result.VenueCount);
        }

        await using var check = new TAGDBContext(options);
        Assert.Equal(new[] { "Bars" }, Names(Value(await CreateCategories(check, null).GetActiveCategories())));

        var venue = Assert.IsType<OkObjectResult>((await CreateController(check, null).GetVenue(1)).Result).Value;
        var categories = ((System.Collections.IEnumerable)Prop(venue, "Categories")!).Cast<object>().ToList();
        var theaters = Assert.Single(categories);
        Assert.Equal("Theaters", Prop(theaters, "Name"));
        Assert.Equal(false, Prop(theaters, "IsActive"));

        Assert.Single(check.Set<Log>().Where(l => l.ShortText == "Venue category deactivated"));
    }

    [Fact]
    public async Task Reactivate_BringsTheCategoryBackAndRepeatedCallsWriteNoExtraAuditEntries()
    {
        var options = await SeedAsync(nameof(Reactivate_BringsTheCategoryBackAndRepeatedCallsWriteNoExtraAuditEntries));
        await using var context = new TAGDBContext(options);
        var controller = CreateCategories(context, StaffId);

        await controller.SetCategoryActive(3, new VenueCategoryActiveRequest { IsActive = true });
        await controller.SetCategoryActive(3, new VenueCategoryActiveRequest { IsActive = true });

        Assert.Contains("Old Spaces", Names(Value(await controller.GetActiveCategories())));
        Assert.Single(context.Set<Log>().Where(l => l.ShortText == "Venue category activated"));
    }

    [Fact]
    public async Task SetActive_ByNonStaffOrForAnUnknownCategory_IsRejected()
    {
        var options = await SeedAsync(nameof(SetActive_ByNonStaffOrForAnUnknownCategory_IsRejected));
        await using var context = new TAGDBContext(options);

        AssertStatus(403, (await CreateCategories(context, OwnerId).SetCategoryActive(1, new VenueCategoryActiveRequest { IsActive = false })).Result);
        Assert.IsType<NotFoundResult>((await CreateCategories(context, StaffId).SetCategoryActive(77, new VenueCategoryActiveRequest { IsActive = false })).Result);
        Assert.True((await context.Set<VenueCategory>().FindAsync(1))!.IsActive);
    }

    // ---- venue categories

    [Fact]
    public async Task SetVenueCategories_ByOwner_ReplacesTheCategories()
    {
        var options = await SeedAsync(nameof(SetVenueCategories_ByOwner_ReplacesTheCategories));
        await using var context = new TAGDBContext(options);

        var result = Value(await CreateCategories(context, OwnerId).SetVenueCategories(1, new VenueCategoriesRequest { CategoryIds = new() { 2, 2 } }));

        Assert.Equal(new[] { "Bars" }, Names(result));
        await using var check = new TAGDBContext(options);
        Assert.Equal(new[] { 2 }, check.Set<Linker_VenueToCategory>().Where(l => l.VenueID == 1).Select(l => l.VenueCategoryID).ToArray());
        var log = await check.Set<Log>().SingleAsync(l => l.ShortText == "Venue categories changed");
        Assert.Equal("venueId=1;actingUserId=10;added=2;removed=1", log.LoggedData);
    }

    [Fact]
    public async Task SetVenueCategories_ByStaff_Works_AndAnEmptyListClearsThem()
    {
        var options = await SeedAsync(nameof(SetVenueCategories_ByStaff_Works_AndAnEmptyListClearsThem));
        await using var context = new TAGDBContext(options);

        var added = Value(await CreateCategories(context, StaffId).SetVenueCategories(1, new VenueCategoriesRequest { CategoryIds = new() { 1, 2 } }));
        Assert.Equal(new[] { "Bars", "Theaters" }, Names(added));

        var cleared = Value(await CreateCategories(context, StaffId).SetVenueCategories(1, new VenueCategoriesRequest { CategoryIds = new() }));
        Assert.Empty(cleared);
        Assert.Empty(context.Set<Linker_VenueToCategory>());
    }

    [Fact]
    public async Task SetVenueCategories_ByAnotherUserOrAnonymous_IsRejected()
    {
        var options = await SeedAsync(nameof(SetVenueCategories_ByAnotherUserOrAnonymous_IsRejected));
        await using var context = new TAGDBContext(options);

        Assert.IsType<UnauthorizedResult>((await CreateCategories(context, null).SetVenueCategories(1, new VenueCategoriesRequest { CategoryIds = new() { 2 } })).Result);
        AssertStatus(403, (await CreateCategories(context, OtherUserId).SetVenueCategories(1, new VenueCategoriesRequest { CategoryIds = new() { 2 } })).Result);
        Assert.Equal(new[] { 1 }, context.Set<Linker_VenueToCategory>().Select(l => l.VenueCategoryID).ToArray());
    }

    [Fact]
    public async Task SetVenueCategories_ForAnUnknownVenue_IsNotFound()
    {
        var options = await SeedAsync(nameof(SetVenueCategories_ForAnUnknownVenue_IsNotFound));
        await using var context = new TAGDBContext(options);

        var result = await CreateCategories(context, StaffId).SetVenueCategories(55, new VenueCategoriesRequest { CategoryIds = new() { 1 } });

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task SetVenueCategories_RejectsMissingBadAndUnknownIds()
    {
        var options = await SeedAsync(nameof(SetVenueCategories_RejectsMissingBadAndUnknownIds));
        await using var context = new TAGDBContext(options);
        var controller = CreateCategories(context, OwnerId);

        AssertStatus(400, (await controller.SetVenueCategories(1, new VenueCategoriesRequest { CategoryIds = null })).Result);
        AssertStatus(400, (await controller.SetVenueCategories(1, new VenueCategoriesRequest { CategoryIds = new() { 0 } })).Result);
        AssertStatus(400, (await controller.SetVenueCategories(1, new VenueCategoriesRequest { CategoryIds = new() { 2, 404 } })).Result);
        Assert.Equal(new[] { 1 }, context.Set<Linker_VenueToCategory>().Select(l => l.VenueCategoryID).ToArray());
    }

    [Fact]
    public async Task SetVenueCategories_CannotAddAnInactiveCategory_ButCanKeepOneItAlreadyHas()
    {
        var options = await SeedAsync(nameof(SetVenueCategories_CannotAddAnInactiveCategory_ButCanKeepOneItAlreadyHas));
        await using (var context = new TAGDBContext(options))
        {
            var rejected = await CreateCategories(context, OwnerId).SetVenueCategories(1, new VenueCategoriesRequest { CategoryIds = new() { 1, 3 } });
            AssertStatus(400, rejected.Result);
        }

        await using (var seed = new TAGDBContext(options))
        {
            (await seed.Set<VenueCategory>().FindAsync(1))!.IsActive = false;
            await seed.SaveChangesAsync();
        }

        await using var check = new TAGDBContext(options);
        var kept = Value(await CreateCategories(check, OwnerId).SetVenueCategories(1, new VenueCategoriesRequest { CategoryIds = new() { 1, 2 } }));
        Assert.Equal(new[] { "Bars", "Theaters" }, Names(kept));
    }

    [Fact]
    public async Task SetVenueCategories_WithNoChange_WritesNoAuditEntry()
    {
        var options = await SeedAsync(nameof(SetVenueCategories_WithNoChange_WritesNoAuditEntry));
        await using var context = new TAGDBContext(options);

        await CreateCategories(context, OwnerId).SetVenueCategories(1, new VenueCategoriesRequest { CategoryIds = new() { 1 } });

        Assert.Empty(context.Set<Log>().Where(l => l.ShortText == "Venue categories changed"));
    }

    // ---- venue API

    [Fact]
    public async Task VenueApi_ReturnsCategories_OnTheSingleVenueTheListAndBySlug()
    {
        var options = await SeedAsync(nameof(VenueApi_ReturnsCategories_OnTheSingleVenueTheListAndBySlug));
        await using var context = new TAGDBContext(options);
        var venues = CreateController(context, null);

        static List<string?> Slugs(object? venue) =>
            ((System.Collections.IEnumerable)Prop(venue, "Categories")!).Cast<object>().Select(c => (string?)Prop(c, "Slug")).ToList();

        var single = Assert.IsType<OkObjectResult>((await venues.GetVenue(1)).Result).Value;
        var bySlug = Assert.IsType<OkObjectResult>((await venues.GetVenueBySlug("terminal-west")).Result).Value;
        var list = ((System.Collections.IEnumerable)Assert.IsType<OkObjectResult>((await venues.GetVenues()).Result).Value!).Cast<object>().Single();

        Assert.Equal(new[] { "theaters" }, Slugs(single));
        Assert.Equal(new[] { "theaters" }, Slugs(bySlug));
        Assert.Equal(new[] { "theaters" }, Slugs(list));
    }

    // ---- staff import category hints

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

    private static VenueImportRequest Hinted(string name, string street, string? hint) => new()
    {
        Venues = new() { new VenueImportItem { Name = name, AddressLine1 = street, City = "Atlanta", State = "GA", CategoryHint = hint } },
    };

    [Theory]
    [InlineData("Bars")]
    [InlineData("bars")]
    [InlineData("BARS")]
    public async Task Import_CategoryHint_LinksANewVenueToTheMatchingActiveCategory(string hint)
    {
        var options = await SeedAsync($"{nameof(Import_CategoryHint_LinksANewVenueToTheMatchingActiveCategory)}-{hint}");
        await using var context = new TAGDBContext(options);

        var response = Value(await CreateImporter(context).ImportVenues(Hinted("The Earl", "488 Flat Shoals Ave SE", hint)));

        var row = Assert.Single(response.Rows);
        Assert.Equal(VenueImportController.StatusCreated, row.Status);
        Assert.Null(row.Message);
        Assert.Equal(new[] { 2 }, context.Set<Linker_VenueToCategory>().Where(l => l.VenueID == row.VenueID).Select(l => l.VenueCategoryID).ToArray());
    }

    [Theory]
    [InlineData("Dance Halls")]
    [InlineData("Old Spaces")]
    public async Task Import_UnknownOrInactiveCategoryHint_CreatesTheVenueWithoutACategoryAndSaysSo(string hint)
    {
        var options = await SeedAsync($"{nameof(Import_UnknownOrInactiveCategoryHint_CreatesTheVenueWithoutACategoryAndSaysSo)}-{hint}");
        await using var context = new TAGDBContext(options);

        var response = Value(await CreateImporter(context).ImportVenues(Hinted("The Earl", "488 Flat Shoals Ave SE", hint)));

        var row = Assert.Single(response.Rows);
        Assert.Equal(VenueImportController.StatusCreated, row.Status);
        Assert.Contains("did not match", row.Message);
        Assert.Empty(context.Set<Linker_VenueToCategory>().Where(l => l.VenueID == row.VenueID));
    }

    [Fact]
    public async Task Import_CategoryHint_FillsInAnUncategorizedUnownedVenueButNeverChangesExistingCategories()
    {
        var options = await SeedAsync(nameof(Import_CategoryHint_FillsInAnUncategorizedUnownedVenueButNeverChangesExistingCategories));
        await using (var seed = new TAGDBContext(options))
        {
            seed.Set<Address>().Add(new Address { AddressID = 901, AddressLine1 = "1 Pine St", City = "Atlanta", State = "GA", ZipCode = "30301", Country = "USA" });
            seed.Set<Venue>().Add(new Venue { VenueID = 5, Name = "Pine Hall", AddressID = 901, IsPublished = true });
            await seed.SaveChangesAsync();
        }

        await using var context = new TAGDBContext(options);

        var first = Value(await CreateImporter(context).ImportVenues(Hinted("Pine Hall", "1 Pine Street", "Bars")));
        Assert.Equal(VenueImportController.StatusUpdated, first.Rows[0].Status);

        var second = Value(await CreateImporter(context).ImportVenues(Hinted("Pine Hall", "1 Pine Street", "Theaters")));
        Assert.Equal(VenueImportController.StatusUnchanged, second.Rows[0].Status);
        Assert.Equal(new[] { 2 }, context.Set<Linker_VenueToCategory>().Where(l => l.VenueID == 5).Select(l => l.VenueCategoryID).ToArray());
    }
}
