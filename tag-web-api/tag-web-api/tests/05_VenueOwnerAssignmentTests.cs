using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Controllers;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;
using Xunit;
using static tagApiUnitTests.VenueTestSupport;

namespace tagApiUnitTests;

// Story #337: staff assign and remove venue owners, and every change is audit-logged.
public class VenueOwnerAssignmentTests
{
    private const int StaffId = 99;
    private const int AnnId = 10;
    private const int BeeId = 20;

    // Venue 1 is owned by Ann; venues 2 and 3 have no owner.
    private static async Task<DbContextOptions<TAGDBContext>> SeedAsync(string databaseName)
    {
        var options = CreateOptions(databaseName);
        await VenueTestSupport.SeedAsync(
            options,
            new Venue { VenueID = 1, Name = "Test Hall", AddressID = AddressId, IsPublished = true },
            AnnId,
            StaffId);

        await using var seed = new TAGDBContext(options);
        seed.Users.Add(new User { UserID = AnnId, EmailOne = "ann@example.com", Username = "ann", FirstName = "Ann", FamName = "Lee", BirthDate = new DateTime(1990, 1, 1), BannedReason = "private" });
        seed.Users.Add(new User { UserID = BeeId, EmailOne = "bee@example.com", Username = "bee", PreferredName = "Bee Keeper" });
        seed.Set<Venue>().Add(new Venue { VenueID = 2, Name = "Second Stage", AddressID = AddressId });
        seed.Set<Venue>().Add(new Venue { VenueID = 3, Name = "<b>Fancy</b> Club", Name_Plaintext = "Fancy Club", AddressID = AddressId });
        await seed.SaveChangesAsync();
        return options;
    }

    private static void AssertForbidden(object? result)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    private static VenueAdminPageDto Page(ActionResult<VenueAdminPageDto> result) =>
        Assert.IsType<VenueAdminPageDto>(Assert.IsType<OkObjectResult>(result.Result).Value);

    // ---- GET admin/venues

    [Fact]
    public async Task AdminVenues_NonStaffAndAnonymous_AreRejected()
    {
        var options = await SeedAsync(nameof(AdminVenues_NonStaffAndAnonymous_AreRejected));
        await using var context = new TAGDBContext(options);

        AssertForbidden((await CreateLinkerController(context, AnnId).GetVenuesForStaff()).Result);
        Assert.IsType<UnauthorizedResult>((await CreateLinkerController(context, userId: null).GetVenuesForStaff()).Result);
    }

    [Fact]
    public async Task AdminVenues_ListsEveryVenueWithItsOwners_AndUnownedOnesHaveNone()
    {
        var options = await SeedAsync(nameof(AdminVenues_ListsEveryVenueWithItsOwners_AndUnownedOnesHaveNone));
        await using var context = new TAGDBContext(options);

        var page = Page(await CreateLinkerController(context, StaffId).GetVenuesForStaff());

        Assert.Equal(new[] { 1, 2, 3 }, page.Items.Select(v => v.VenueID).ToArray());
        Assert.Null(page.NextAfterId);

        var owned = page.Items[0];
        Assert.Equal("test-hall", owned.Slug);
        var owner = Assert.Single(owned.Owners);
        Assert.Equal(AnnId, owner.UserID);
        Assert.Equal("Ann Lee", owner.DisplayName);
        Assert.Equal("ann@example.com", owner.Email);
        Assert.Equal(Linker_UserToVenue.OwnerRole, owner.Role);

        Assert.Empty(page.Items[1].Owners);
        Assert.Equal("Fancy Club", page.Items[2].Name);
    }

    [Fact]
    public async Task AdminVenues_UnownedOnly_ReturnsJustTheVenuesWithoutOwners()
    {
        var options = await SeedAsync(nameof(AdminVenues_UnownedOnly_ReturnsJustTheVenuesWithoutOwners));
        await using var context = new TAGDBContext(options);

        var page = Page(await CreateLinkerController(context, StaffId).GetVenuesForStaff(unownedOnly: true));

        Assert.Equal(new[] { 2, 3 }, page.Items.Select(v => v.VenueID).ToArray());
    }

    [Theory]
    [InlineData("FANCY", 3)]
    [InlineData("second", 2)]
    [InlineData("  test ", 1)]
    public async Task AdminVenues_Search_IsCaseInsensitiveAndUsesThePlaintextName(string search, int expectedVenueId)
    {
        var options = await SeedAsync($"{nameof(AdminVenues_Search_IsCaseInsensitiveAndUsesThePlaintextName)}-{expectedVenueId}");
        await using var context = new TAGDBContext(options);

        var page = Page(await CreateLinkerController(context, StaffId).GetVenuesForStaff(search: search));

        Assert.Equal(expectedVenueId, Assert.Single(page.Items).VenueID);
    }

    [Fact]
    public async Task AdminVenues_PagesWithACursor_AndClampsTheLimit()
    {
        var options = await SeedAsync(nameof(AdminVenues_PagesWithACursor_AndClampsTheLimit));
        await using var context = new TAGDBContext(options);
        var controller = CreateLinkerController(context, StaffId);

        var first = Page(await controller.GetVenuesForStaff(limit: 2));
        Assert.Equal(new[] { 1, 2 }, first.Items.Select(v => v.VenueID).ToArray());
        Assert.Equal(2, first.NextAfterId);

        var second = Page(await controller.GetVenuesForStaff(afterId: first.NextAfterId!.Value, limit: 2));
        Assert.Equal(new[] { 3 }, second.Items.Select(v => v.VenueID).ToArray());
        Assert.Null(second.NextAfterId);

        Assert.Single(Page(await controller.GetVenuesForStaff(limit: 0)).Items);
        Assert.Equal(3, Page(await controller.GetVenuesForStaff(limit: 100000, afterId: -5)).Items.Count);
    }

    // ---- GET admin/users

    [Fact]
    public async Task AdminUsers_IsStaffOnly()
    {
        var options = await SeedAsync(nameof(AdminUsers_IsStaffOnly));
        await using var context = new TAGDBContext(options);

        AssertForbidden((await CreateLinkerController(context, AnnId).FindUsersForStaff("ann")).Result);
        Assert.IsType<UnauthorizedResult>((await CreateLinkerController(context, userId: null).FindUsersForStaff("ann")).Result);
    }

    [Fact]
    public async Task AdminUsers_FindsByNameUsernameOrEmail_AndExposesOnlyTheFieldsStaffNeed()
    {
        var options = await SeedAsync(nameof(AdminUsers_FindsByNameUsernameOrEmail_AndExposesOnlyTheFieldsStaffNeed));
        await using var context = new TAGDBContext(options);
        var controller = CreateLinkerController(context, StaffId);

        foreach (var term in new[] { "ann", "LEE", "bee@example", "keeper" })
        {
            var ok = Assert.IsType<OkObjectResult>((await controller.FindUsersForStaff(term)).Result);
            var found = Assert.IsAssignableFrom<IEnumerable<UserLookupDto>>(ok.Value).ToList();
            Assert.Single(found);
        }

        var ann = Assert.IsAssignableFrom<IEnumerable<UserLookupDto>>(Assert.IsType<OkObjectResult>((await controller.FindUsersForStaff("ann")).Result).Value).Single();
        Assert.Equal("Ann Lee", ann.DisplayName);
        Assert.Equal("ann@example.com", ann.Email);

        // Birth date, ban reason and every other private field stay out of the response.
        var exposed = typeof(UserLookupDto).GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "DisplayName", "Email", "UserID", "Username" }, exposed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("   ")]
    public async Task AdminUsers_ShortOrEmptySearch_ReturnsNothing(string? search)
    {
        var options = await SeedAsync($"{nameof(AdminUsers_ShortOrEmptySearch_ReturnsNothing)}-{search?.Length}");
        await using var context = new TAGDBContext(options);

        var ok = Assert.IsType<OkObjectResult>((await CreateLinkerController(context, StaffId).FindUsersForStaff(search)).Result);

        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<UserLookupDto>>(ok.Value));
    }

    // ---- POST venue/{venueId}/owners

    [Fact]
    public async Task AssignOwner_ByStaff_LinksTheUserAndWritesAnAuditEntry()
    {
        var options = await SeedAsync(nameof(AssignOwner_ByStaff_LinksTheUserAndWritesAnAuditEntry));
        await using var context = new TAGDBContext(options);

        var result = await CreateLinkerController(context, StaffId).AssignOwner(2, new VenueOwnerAssignRequest { UserID = BeeId });

        var created = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        var dto = Assert.IsType<VenueOwnerDto>(created.Value);
        Assert.Equal(BeeId, dto.UserID);
        Assert.Equal("Bee Keeper", dto.DisplayName);

        await using var verify = new TAGDBContext(options);
        var link = await verify.Set<Linker_UserToVenue>().SingleAsync(l => l.VenueID == 2);
        Assert.Equal(BeeId, link.UserID);
        Assert.Equal(Linker_UserToVenue.OwnerRole, link.Role);

        var log = await verify.Set<Log>().SingleAsync();
        Assert.Equal("Venue owner assigned", log.ShortText);
        Assert.Equal(StaffId, log.UserID);
        Assert.Contains("operation=assign", log.Tags);
        Assert.Equal($"venueId=2;ownerUserId={BeeId};actingUserId={StaffId}", log.LoggedData);
    }

    [Fact]
    public async Task AssignOwner_ByNonStaffOrAnonymous_IsRejectedEvenForTheCallersOwnAccount()
    {
        var options = await SeedAsync(nameof(AssignOwner_ByNonStaffOrAnonymous_IsRejectedEvenForTheCallersOwnAccount));
        await using var context = new TAGDBContext(options);

        AssertForbidden((await CreateLinkerController(context, BeeId).AssignOwner(2, new VenueOwnerAssignRequest { UserID = BeeId })).Result);
        AssertForbidden((await CreateLinkerController(context, AnnId).AssignOwner(2, new VenueOwnerAssignRequest { UserID = AnnId })).Result);
        Assert.IsType<UnauthorizedResult>((await CreateLinkerController(context, userId: null).AssignOwner(2, new VenueOwnerAssignRequest { UserID = BeeId })).Result);

        await using var verify = new TAGDBContext(options);
        Assert.Single(verify.Set<Linker_UserToVenue>());
        Assert.Empty(verify.Set<Log>());
    }

    [Fact]
    public async Task AssignOwner_UnknownVenueOrUser_Returns404AndInvalidIdsReturn400()
    {
        var options = await SeedAsync(nameof(AssignOwner_UnknownVenueOrUser_Returns404AndInvalidIdsReturn400));
        await using var context = new TAGDBContext(options);
        var controller = CreateLinkerController(context, StaffId);

        Assert.IsType<NotFoundObjectResult>((await controller.AssignOwner(77, new VenueOwnerAssignRequest { UserID = BeeId })).Result);
        Assert.IsType<NotFoundObjectResult>((await controller.AssignOwner(2, new VenueOwnerAssignRequest { UserID = 4040 })).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.AssignOwner(2, new VenueOwnerAssignRequest { UserID = 0 })).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.AssignOwner(0, new VenueOwnerAssignRequest { UserID = BeeId })).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.AssignOwner(2, null!)).Result);

        await using var verify = new TAGDBContext(options);
        Assert.Single(verify.Set<Linker_UserToVenue>());
        Assert.Empty(verify.Set<Log>());
    }

    [Fact]
    public async Task AssignOwner_AnExistingOwner_Returns409WithoutAnotherLinkOrLogEntry()
    {
        var options = await SeedAsync(nameof(AssignOwner_AnExistingOwner_Returns409WithoutAnotherLinkOrLogEntry));
        await using var context = new TAGDBContext(options);

        var result = await CreateLinkerController(context, StaffId).AssignOwner(1, new VenueOwnerAssignRequest { UserID = AnnId });

        Assert.IsType<ConflictObjectResult>(result.Result);
        await using var verify = new TAGDBContext(options);
        Assert.Single(verify.Set<Linker_UserToVenue>());
        Assert.Empty(verify.Set<Log>());
    }

    [Fact]
    public async Task AssignOwner_ANewOwnerSeesTheVenueAndCanEditItOnlyAfterTheAssignment()
    {
        var options = await SeedAsync(nameof(AssignOwner_ANewOwnerSeesTheVenueAndCanEditItOnlyAfterTheAssignment));
        await using var context = new TAGDBContext(options);
        var edit = new VenueUpdateRequest { VenueID = 2, Name = "Bee's Stage", AddressID = AddressId };

        AssertForbidden(await CreateController(context, BeeId).PutVenue(2, edit));
        var before = Assert.IsType<OkObjectResult>((await CreateLinkerController(context, BeeId).GetVenuesByUserId(BeeId)).Result);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<object>>(before.Value));

        await CreateLinkerController(context, StaffId).AssignOwner(2, new VenueOwnerAssignRequest { UserID = BeeId });

        var after = Assert.IsType<OkObjectResult>((await CreateLinkerController(context, BeeId).GetVenuesByUserId(BeeId)).Result);
        Assert.Equal("Second Stage", Prop(Assert.Single(Assert.IsAssignableFrom<IEnumerable<object>>(after.Value)), "Name"));
        Assert.IsType<NoContentResult>(await CreateController(context, BeeId).PutVenue(2, edit));
    }

    // ---- DELETE venue/{venueId}/owners/{userId}

    [Fact]
    public async Task RemoveOwner_ByStaff_UnlinksTheUserAndWritesAnAuditEntry()
    {
        var options = await SeedAsync(nameof(RemoveOwner_ByStaff_UnlinksTheUserAndWritesAnAuditEntry));
        await using var context = new TAGDBContext(options);

        var result = await CreateLinkerController(context, StaffId).RemoveOwner(1, AnnId);

        Assert.IsType<NoContentResult>(result);
        await using var verify = new TAGDBContext(options);
        Assert.Empty(verify.Set<Linker_UserToVenue>());
        var log = await verify.Set<Log>().SingleAsync();
        Assert.Equal("Venue owner removed", log.ShortText);
        Assert.Contains("operation=remove", log.Tags);
        Assert.Equal($"venueId=1;ownerUserId={AnnId};actingUserId={StaffId}", log.LoggedData);
        Assert.Equal(StaffId, log.UserID);
    }

    [Fact]
    public async Task RemoveOwner_TheFormerOwnerLosesAccess()
    {
        var options = await SeedAsync(nameof(RemoveOwner_TheFormerOwnerLosesAccess));
        await using var context = new TAGDBContext(options);
        var edit = new VenueUpdateRequest { VenueID = 1, Name = "Test Hall", AddressID = AddressId };
        Assert.IsType<NoContentResult>(await CreateController(context, AnnId).PutVenue(1, edit));

        await CreateLinkerController(context, StaffId).RemoveOwner(1, AnnId);

        AssertForbidden(await CreateController(context, AnnId).PutVenue(1, edit));
        var list = Assert.IsType<OkObjectResult>((await CreateLinkerController(context, AnnId).GetVenuesByUserId(AnnId)).Result);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<object>>(list.Value));
    }

    [Fact]
    public async Task RemoveOwner_ByNonStaff_IsRejectedAndMissingLinksReturn404()
    {
        var options = await SeedAsync(nameof(RemoveOwner_ByNonStaff_IsRejectedAndMissingLinksReturn404));
        await using var context = new TAGDBContext(options);

        AssertForbidden(await CreateLinkerController(context, AnnId).RemoveOwner(1, AnnId));
        Assert.IsType<UnauthorizedResult>(await CreateLinkerController(context, userId: null).RemoveOwner(1, AnnId));
        Assert.IsType<NotFoundObjectResult>(await CreateLinkerController(context, StaffId).RemoveOwner(1, BeeId));

        await using var verify = new TAGDBContext(options);
        Assert.Single(verify.Set<Linker_UserToVenue>());
        Assert.Empty(verify.Set<Log>());
    }

    [Fact]
    public async Task ReassigningAVenue_IsARemovalPlusAnAssignment_AndEachStepIsLogged()
    {
        var options = await SeedAsync(nameof(ReassigningAVenue_IsARemovalPlusAnAssignment_AndEachStepIsLogged));
        await using var context = new TAGDBContext(options);
        var staff = CreateLinkerController(context, StaffId);

        await staff.RemoveOwner(1, AnnId);
        await staff.AssignOwner(1, new VenueOwnerAssignRequest { UserID = BeeId });

        var page = Page(await staff.GetVenuesForStaff(search: "test hall"));
        Assert.Equal(BeeId, Assert.Single(Assert.Single(page.Items).Owners).UserID);
        await using var verify = new TAGDBContext(options);
        var logged = verify.Set<Log>().Select(l => l.ShortText).ToList().OrderBy(text => text, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "Venue owner assigned", "Venue owner removed" }, logged);
    }
}
