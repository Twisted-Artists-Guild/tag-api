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

// Story #328: artists use the shared regions. The old free-text region is mapped to regions where it matches exactly, the
// rest is reported and left alone. Database names start with "artistregion-" because they are shared across test classes.
public class ArtistRegionTests
{
    private const int StaffId = 99;
    private const int OwnerId = 10;
    private const int OtherUserId = 11;

    // 1 Georgia (statewide GA), 2 North Carolina (statewide NC), 3 Atlanta Metro (within GA), 4 Gulf Coast (multistate TX, LA),
    // 5 Alabama (statewide AL, inactive), 6 Hudson and Delaware (multistate NY, NJ, DE), 7 Old Area (within GA, inactive).
    private static List<Region> Regions() => new()
    {
        new Region { RegionID = 1, Name = "Georgia", Slug = "georgia", Type = RegionTypes.Statewide, States = new() { "GA" }, IsActive = true },
        new Region { RegionID = 2, Name = "North Carolina", Slug = "north-carolina", Type = RegionTypes.Statewide, States = new() { "NC" }, IsActive = true },
        new Region { RegionID = 3, Name = "Atlanta Metro", Slug = "atlanta-metro", Type = RegionTypes.WithinState, States = new() { "GA" }, IsActive = true },
        new Region { RegionID = 4, Name = "Gulf Coast", Slug = "gulf-coast", Type = RegionTypes.Multistate, States = new() { "TX", "LA" }, IsActive = true },
        new Region { RegionID = 5, Name = "Alabama", Slug = "alabama", Type = RegionTypes.Statewide, States = new() { "AL" }, IsActive = false },
        new Region { RegionID = 6, Name = "Hudson and Delaware", Slug = "hudson-and-delaware", Type = RegionTypes.Multistate, States = new() { "NY", "NJ", "DE" }, IsActive = true },
        new Region { RegionID = 7, Name = "Old Area", Slug = "old-area", Type = RegionTypes.WithinState, States = new() { "GA" }, IsActive = false },
    };

    // ---- the mapping

    [Theory]
    [InlineData("GA", new[] { 1 })]
    [InlineData("ga", new[] { 1 })]
    [InlineData("G.A.", new[] { 1 })]
    [InlineData("Georgia", new[] { 1 })]
    [InlineData("  georgia  ", new[] { 1 })]
    [InlineData("North  Carolina", new[] { 2 })]
    [InlineData("north-carolina", new[] { 2 })]
    [InlineData("NC", new[] { 2 })]
    [InlineData("Atlanta Metro", new[] { 3 })]
    [InlineData("atlanta-metro", new[] { 3 })]
    [InlineData("Gulf Coast", new[] { 4 })]
    [InlineData("Hudson and Delaware", new[] { 6 })]
    public void Map_FindsARegionByNameSlugOrState(string text, int[] expected)
    {
        var match = RegionTextMapper.Map(text, Regions());

        Assert.Equal(expected, match.RegionIds);
        Assert.Empty(match.Unmatched);
    }

    [Theory]
    [InlineData("Georgia, North Carolina", new[] { 1, 2 })]
    [InlineData("GA/NC", new[] { 1, 2 })]
    [InlineData("GA; NC; GA", new[] { 1, 2 })]
    [InlineData("Georgia and North Carolina", new[] { 1, 2 })]
    [InlineData("Georgia & Atlanta Metro", new[] { 1, 3 })]
    [InlineData("GA | NC", new[] { 1, 2 })]
    public void Map_SplitsSeveralRegionsInOneText(string text, int[] expected)
    {
        var match = RegionTextMapper.Map(text, Regions());

        Assert.Equal(expected, match.RegionIds);
        Assert.Empty(match.Unmatched);
    }

    [Fact]
    public void Map_KeepsWhatItCannotPlace_AndStillLinksTheRest()
    {
        var match = RegionTextMapper.Map("Georgia, Narnia", Regions());

        Assert.Equal(new[] { 1 }, match.RegionIds);
        Assert.Equal(new[] { "Narnia" }, match.Unmatched);
    }

    [Theory]
    [InlineData("Atlanta")] // close to "Atlanta Metro", but only exact matches are made
    [InlineData("Southeast")]
    [InlineData("AL")] // its statewide region is inactive
    [InlineData("Alabama")]
    [InlineData("Old Area")] // inactive
    [InlineData("<b>GA</b>")]
    [InlineData("Georgia USA")]
    public void Map_NeverGuesses(string text)
    {
        var match = RegionTextMapper.Map(text, Regions());

        Assert.Empty(match.RegionIds);
        Assert.Equal(new[] { text }, match.Unmatched);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Map_IgnoresEmptyText(string? text)
    {
        var match = RegionTextMapper.Map(text, Regions());

        Assert.Empty(match.RegionIds);
        Assert.Empty(match.Unmatched);
    }

    [Fact]
    public void Map_ReportsEveryUnmatchedPartOnce()
    {
        var match = RegionTextMapper.Map("Narnia, narnia, Mordor", Regions());

        Assert.Empty(match.RegionIds);
        Assert.Equal(new[] { "Narnia", "Mordor" }, match.Unmatched);
    }

    [Fact]
    public void Map_PrefersAnExactNameOverAState()
    {
        var regions = Regions();
        regions.Add(new Region { RegionID = 8, Name = "GA", Slug = "ga-region", Type = RegionTypes.WithinState, States = new() { "GA" }, IsActive = true });

        Assert.Equal(new[] { 8 }, RegionTextMapper.Map("GA", regions).RegionIds);
    }

    // ---- the migration

    private static async Task<DbContextOptions<TAGDBContext>> SeedMigrationAsync(string name)
    {
        var options = CreateOptions($"artistregion-{name}");
        await using var seed = new TAGDBContext(options);
        seed.Set<Region>().AddRange(Regions());
        for (var id = 1; id <= 8; id++)
        {
            seed.Artists.Add(new Artist { ArtistID = id, Title = $"Artist {id}", Path = $"artist-{id}", Applied = DateTime.UtcNow, Since = DateTime.UtcNow });
        }

        var next = 100;
        void Address(int artistId, string? region, ContactScope scope = ContactScope.Secondary, string type = "address", int? addressId = null)
        {
            var id = addressId ?? ++next;
            seed.Set<Address>().Add(new Address { AddressID = id, AddressLine1 = "1 Main St", City = "Town", Country = "USA", Region = region });
            seed.Set<Contact>().Add(new Contact { ContactID = id, ContactType = type, AddressID = id });
            seed.Set<Linker_EntityToContact>().Add(new Linker_EntityToContact { ContactID = id, EntityType = LinkedEntityTypes.Artist, EntityID = artistId, Scope = scope });
        }

        Address(1, "GA");
        Address(2, "Georgia, Narnia");
        Address(3, "GA", ContactScope.Private);
        Address(4, "GA");
        seed.Set<Linker_ArtistToRegion>().Add(new Linker_ArtistToRegion { ArtistID = 4, RegionID = 2 });
        Address(5, "Narnia");
        Address(6, "narnia");
        Address(8, "NC", type: "phone");
        Address(99, "GA");
        Address(7, string.Empty);
        await seed.SaveChangesAsync();
        return options;
    }

    private static async Task<List<(int Artist, int Region)>> LinksAsync(DbContextOptions<TAGDBContext> options)
    {
        await using var read = new TAGDBContext(options);
        return (await read.Set<Linker_ArtistToRegion>().OrderBy(l => l.ArtistID).ThenBy(l => l.RegionID).ToListAsync()).Select(l => (l.ArtistID, l.RegionID)).ToList();
    }

    [Fact]
    public async Task Migration_PreviewReportsEverythingAndChangesNothing()
    {
        var options = await SeedMigrationAsync(nameof(Migration_PreviewReportsEverythingAndChangesNothing));
        await using var context = new TAGDBContext(options);

        var report = await ArtistRegionMigration.RunAsync(context, apply: false, actingUserId: StaffId);

        Assert.True(report.DryRun);
        Assert.Equal(5, report.ArtistsWithRegionText); // artists 1, 2, 4, 5, 6; not 3 (private), 7 (empty), 8 (not an address) or 99 (no such artist)
        Assert.Equal(1, report.ArtistsSkippedAlreadyHadRegions);
        Assert.Equal(2, report.ArtistsMigrated);
        Assert.Equal(2, report.LinksAdded);
        Assert.Equal(2, report.ArtistsWithNoMatch);
        Assert.Equal(new[] { (4, 2) }, await LinksAsync(options));
    }

    [Fact]
    public async Task Migration_ReportsUnmatchedTextWithTheArtistsToLookAt()
    {
        var options = await SeedMigrationAsync(nameof(Migration_ReportsUnmatchedTextWithTheArtistsToLookAt));
        await using var context = new TAGDBContext(options);

        var report = await ArtistRegionMigration.RunAsync(context, apply: false, actingUserId: StaffId);

        var narnia = Assert.Single(report.Unmatched);
        Assert.Equal("Narnia", narnia.Text);
        Assert.Equal(3, narnia.ArtistCount);
        Assert.Equal(new[] { 2, 5, 6 }, narnia.ArtistIds);
    }

    [Fact]
    public async Task Migration_Apply_LinksOnlyTheMatches_AndNeverTouchesTheAddressText()
    {
        var options = await SeedMigrationAsync(nameof(Migration_Apply_LinksOnlyTheMatches_AndNeverTouchesTheAddressText));
        await using var context = new TAGDBContext(options);

        var report = await ArtistRegionMigration.RunAsync(context, apply: true, actingUserId: StaffId);

        Assert.False(report.DryRun);
        Assert.Equal(new[] { (1, 1), (2, 1), (4, 2) }, await LinksAsync(options));
        await using var verify = new TAGDBContext(options);
        var texts = await verify.Set<Address>().Where(a => a.Region != null).OrderBy(a => a.AddressID).Select(a => a.Region).ToListAsync();
        Assert.Contains("Georgia, Narnia", texts);
        Assert.Contains("narnia", texts);
        Assert.Equal(1, await verify.Set<Log>().CountAsync(l => l.Tags!.Contains("operation=migrate")));
    }

    [Fact]
    public async Task Migration_CanRunAgain_AndAddsNothingTheSecondTime()
    {
        var options = await SeedMigrationAsync(nameof(Migration_CanRunAgain_AndAddsNothingTheSecondTime));
        await using (var first = new TAGDBContext(options))
        {
            await ArtistRegionMigration.RunAsync(first, apply: true, actingUserId: StaffId);
        }

        await using var context = new TAGDBContext(options);
        var second = await ArtistRegionMigration.RunAsync(context, apply: true, actingUserId: StaffId);

        Assert.Equal(0, second.ArtistsMigrated);
        Assert.Equal(0, second.LinksAdded);
        Assert.Equal(3, second.ArtistsSkippedAlreadyHadRegions);
        Assert.Equal(new[] { (1, 1), (2, 1), (4, 2) }, await LinksAsync(options));
    }

    [Fact]
    public async Task Migration_NeverChangesAnArtistWhoAlreadyChoseRegions()
    {
        var options = await SeedMigrationAsync(nameof(Migration_NeverChangesAnArtistWhoAlreadyChoseRegions));
        await using var context = new TAGDBContext(options);

        await ArtistRegionMigration.RunAsync(context, apply: true, actingUserId: StaffId);

        Assert.DoesNotContain((4, 1), await LinksAsync(options));
    }

    [Fact]
    public async Task Migration_DoesNotReadPrivateAddresses()
    {
        var options = await SeedMigrationAsync(nameof(Migration_DoesNotReadPrivateAddresses));
        await using var context = new TAGDBContext(options);

        await ArtistRegionMigration.RunAsync(context, apply: true, actingUserId: StaffId);

        Assert.DoesNotContain(await LinksAsync(options), link => link.Artist == 3);
    }

    // ---- choosing regions

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

    // Artist 1 is owned by OwnerId and is in Georgia and Old Area (inactive). StaffId is staff; OtherUserId is nobody.
    private static async Task<DbContextOptions<TAGDBContext>> SeedArtistAsync(string name)
    {
        var options = CreateOptions($"artistregion-{name}");
        await using var seed = new TAGDBContext(options);
        seed.Users.AddRange(
            new User { UserID = StaffId, EmailOne = "staff@example.com", Username = "staff", Moderator = true },
            new User { UserID = OwnerId, EmailOne = "owner@example.com", Username = "owner" },
            new User { UserID = OtherUserId, EmailOne = "other@example.com", Username = "other" });
        seed.Set<Region>().AddRange(Regions());
        seed.Artists.Add(new Artist { ArtistID = 1, Title = "Artist 1", Path = "artist-1", Applied = DateTime.UtcNow, Since = DateTime.UtcNow, IsPublished = true });
        seed.Set<Linker_UserToArtist>().Add(new Linker_UserToArtist { UserID = OwnerId, ArtistID = 1, Role = "Owner" });
        seed.Set<Linker_ArtistToRegion>().AddRange(new Linker_ArtistToRegion { ArtistID = 1, RegionID = 1 }, new Linker_ArtistToRegion { ArtistID = 1, RegionID = 7 });
        await seed.SaveChangesAsync();
        return options;
    }

    private static async Task<List<int>> RegionIdsAsync(DbContextOptions<TAGDBContext> options, int artistId = 1)
    {
        await using var read = new TAGDBContext(options);
        return await read.Set<Linker_ArtistToRegion>().Where(l => l.ArtistID == artistId).Select(l => l.RegionID).OrderBy(id => id).ToListAsync();
    }

    private static ArtistRegionsRequest Ids(params int[] ids) => new() { RegionIds = ids.ToList() };

    private static void AssertStatus(int expected, object? result) =>
        Assert.Equal(expected, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode ?? 0);

    [Fact]
    public async Task SetRegions_ByTheArtistsOwner_ReplacesTheRegions()
    {
        var options = await SeedArtistAsync(nameof(SetRegions_ByTheArtistsOwner_ReplacesTheRegions));
        await using var context = new TAGDBContext(options);

        var result = await CreateRegions(context, OwnerId).SetArtistRegions(1, Ids(2, 3));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(new[] { "Atlanta Metro", "North Carolina" }, ((IEnumerable<RegionDto>)ok.Value!).Select(r => r.Name).ToList());
        Assert.Equal(new[] { 2, 3 }, await RegionIdsAsync(options));
    }

    [Fact]
    public async Task SetRegions_ByStaff_CanChangeAnyArtist()
    {
        var options = await SeedArtistAsync(nameof(SetRegions_ByStaff_CanChangeAnyArtist));
        await using var context = new TAGDBContext(options);

        Assert.IsType<OkObjectResult>((await CreateRegions(context, StaffId).SetArtistRegions(1, Ids(2))).Result);

        Assert.Equal(new[] { 2 }, await RegionIdsAsync(options));
    }

    [Fact]
    public async Task SetRegions_ByAStranger_Returns403AndChangesNothing()
    {
        var options = await SeedArtistAsync(nameof(SetRegions_ByAStranger_Returns403AndChangesNothing));
        await using var context = new TAGDBContext(options);

        var result = await CreateRegions(context, OtherUserId).SetArtistRegions(1, Ids(2));

        AssertStatus(StatusCodes.Status403Forbidden, result.Result);
        Assert.Equal(new[] { 1, 7 }, await RegionIdsAsync(options));
    }

    [Fact]
    public async Task SetRegions_PermissionIsCheckedBeforeTheBodyIsValidated()
    {
        var options = await SeedArtistAsync(nameof(SetRegions_PermissionIsCheckedBeforeTheBodyIsValidated));
        await using var context = new TAGDBContext(options);

        var result = await CreateRegions(context, OtherUserId).SetArtistRegions(1, Ids(999));

        AssertStatus(StatusCodes.Status403Forbidden, result.Result);
    }

    [Fact]
    public async Task SetRegions_Anonymous_Returns401_AndAMissingArtistReturns404()
    {
        var options = await SeedArtistAsync(nameof(SetRegions_Anonymous_Returns401_AndAMissingArtistReturns404));
        await using var context = new TAGDBContext(options);

        Assert.IsType<UnauthorizedResult>((await CreateRegions(context, null).SetArtistRegions(1, Ids(1))).Result);
        Assert.IsType<NotFoundResult>((await CreateRegions(context, OwnerId).SetArtistRegions(404, Ids(1))).Result);
    }

    [Fact]
    public async Task SetRegions_AnEmptyListClearsThem_ButAMissingListIsRefused()
    {
        var options = await SeedArtistAsync(nameof(SetRegions_AnEmptyListClearsThem_ButAMissingListIsRefused));
        await using var context = new TAGDBContext(options);
        var controller = CreateRegions(context, OwnerId);

        AssertStatus(StatusCodes.Status400BadRequest, (await controller.SetArtistRegions(1, new ArtistRegionsRequest())).Result);
        AssertStatus(StatusCodes.Status400BadRequest, (await controller.SetArtistRegions(1, null!)).Result);
        Assert.Equal(new[] { 1, 7 }, await RegionIdsAsync(options));

        Assert.IsType<OkObjectResult>((await controller.SetArtistRegions(1, Ids())).Result);
        Assert.Empty(await RegionIdsAsync(options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(999)]
    public async Task SetRegions_RefusesBadIds(int id)
    {
        var options = await SeedArtistAsync($"{nameof(SetRegions_RefusesBadIds)}-{id}");
        await using var context = new TAGDBContext(options);

        AssertStatus(StatusCodes.Status400BadRequest, (await CreateRegions(context, OwnerId).SetArtistRegions(1, Ids(1, id))).Result);
        Assert.Equal(new[] { 1, 7 }, await RegionIdsAsync(options));
    }

    [Fact]
    public async Task SetRegions_ADeactivatedRegionCanStay_ButCannotBeAddedNew()
    {
        var options = await SeedArtistAsync(nameof(SetRegions_ADeactivatedRegionCanStay_ButCannotBeAddedNew));
        await using var context = new TAGDBContext(options);
        var controller = CreateRegions(context, OwnerId);

        AssertStatus(StatusCodes.Status400BadRequest, (await controller.SetArtistRegions(1, Ids(1, 5))).Result);
        Assert.IsType<OkObjectResult>((await controller.SetArtistRegions(1, Ids(1, 2, 7))).Result);
        Assert.Equal(new[] { 1, 2, 7 }, await RegionIdsAsync(options));
    }

    [Fact]
    public async Task SetRegions_DoesNotTouchAnotherArtist_AndWritesAnAuditRow()
    {
        var options = await SeedArtistAsync(nameof(SetRegions_DoesNotTouchAnotherArtist_AndWritesAnAuditRow));
        await using (var seed = new TAGDBContext(options))
        {
            seed.Artists.Add(new Artist { ArtistID = 2, Title = "Artist 2", Path = "artist-2", Applied = DateTime.UtcNow, Since = DateTime.UtcNow });
            seed.Set<Linker_ArtistToRegion>().Add(new Linker_ArtistToRegion { ArtistID = 2, RegionID = 4 });
            await seed.SaveChangesAsync();
        }

        await using var context = new TAGDBContext(options);
        await CreateRegions(context, OwnerId).SetArtistRegions(1, Ids(2));

        Assert.Equal(new[] { 4 }, await RegionIdsAsync(options, artistId: 2));
        await using var verify = new TAGDBContext(options);
        Assert.Equal(1, await verify.Set<Log>().CountAsync(l => l.Tags!.Contains("entity=artist;event=region;operation=set")));
    }

    // ---- the migration endpoint

    [Fact]
    public async Task MigrateEndpoint_IsStaffOnly_AndPreviewsByDefault()
    {
        var options = await SeedMigrationAsync(nameof(MigrateEndpoint_IsStaffOnly_AndPreviewsByDefault));
        await using (var seed = new TAGDBContext(options))
        {
            seed.Users.AddRange(
                new User { UserID = StaffId, EmailOne = "staff@example.com", Username = "staff", Moderator = true },
                new User { UserID = OtherUserId, EmailOne = "other@example.com", Username = "other" });
            await seed.SaveChangesAsync();
        }

        await using var context = new TAGDBContext(options);

        Assert.IsType<UnauthorizedResult>((await CreateRegions(context, null).MigrateArtistRegions()).Result);
        AssertStatus(StatusCodes.Status403Forbidden, (await CreateRegions(context, OtherUserId).MigrateArtistRegions(dryRun: false)).Result);
        Assert.Equal(new[] { (4, 2) }, await LinksAsync(options));

        var preview = Assert.IsType<ArtistRegionMigrationReport>(Assert.IsType<OkObjectResult>((await CreateRegions(context, StaffId).MigrateArtistRegions()).Result).Value);
        Assert.True(preview.DryRun);
        Assert.Equal(new[] { (4, 2) }, await LinksAsync(options));

        var applied = Assert.IsType<ArtistRegionMigrationReport>(Assert.IsType<OkObjectResult>((await CreateRegions(context, StaffId).MigrateArtistRegions(dryRun: false)).Result).Value);
        Assert.False(applied.DryRun);
        Assert.Equal(new[] { (1, 1), (2, 1), (4, 2) }, await LinksAsync(options));
    }

    // ---- the artist as the pages read it

    private static ArtistController CreateArtists(TAGDBContext context) =>
        new(context, NullLogger<ArtistController>.Instance);

    [Fact]
    public async Task ArtistReads_IncludeTheArtistsRegions()
    {
        var options = await SeedArtistAsync(nameof(ArtistReads_IncludeTheArtistsRegions));
        await using var context = new TAGDBContext(options);
        var controller = CreateArtists(context);

        var byId = Assert.IsType<OkObjectResult>((await controller.GetByID(1)).Result);
        var bySlug = Assert.IsType<OkObjectResult>((await controller.Get("artist-1")).Result);

        foreach (var response in new[] { byId, bySlug })
        {
            var regions = Assert.IsAssignableFrom<IEnumerable<RegionDto>>(Prop(response.Value, "Regions"));
            Assert.Equal(new[] { "Georgia", "Old Area" }, regions.Select(r => r.Name).ToList());
            Assert.Equal(new[] { true, false }, regions.Select(r => r.IsActive).ToList());
        }
    }

    [Fact]
    public async Task ArtistReads_ForAnArtistWithNoRegions_ReturnAnEmptyList()
    {
        var options = await SeedArtistAsync(nameof(ArtistReads_ForAnArtistWithNoRegions_ReturnAnEmptyList));
        await using (var seed = new TAGDBContext(options))
        {
            seed.Artists.Add(new Artist { ArtistID = 2, Title = "Artist 2", Path = "artist-2", Applied = DateTime.UtcNow, Since = DateTime.UtcNow, IsPublished = true });
            await seed.SaveChangesAsync();
        }

        await using var context = new TAGDBContext(options);
        var ok = Assert.IsType<OkObjectResult>((await CreateArtists(context).GetByID(2)).Result);

        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<RegionDto>>(Prop(ok.Value, "Regions")));
    }
}
