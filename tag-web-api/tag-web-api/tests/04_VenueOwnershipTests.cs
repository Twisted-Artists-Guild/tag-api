using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Controllers;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;
using Xunit;
using static tagApiUnitTests.VenueTestSupport;

namespace tagApiUnitTests;

// Story #329: who may see and change a venue. Owners and staff manage a venue, everyone else is rejected,
// and the caller's identity always comes from the token.
public class VenueOwnershipTests
{
    private const int OwnerId = 10;
    private const int OtherUserId = 20;
    private const int StaffId = 99;

    private static async Task<DbContextOptions<TAGDBContext>> SeedHallAsync(
        string databaseName,
        bool published = false,
        bool blocked = false,
        int? ownerUserId = OwnerId,
        int? logoPicId = null)
    {
        var options = CreateOptions(databaseName);
        await SeedAsync(
            options,
            new Venue { VenueID = 1, Name = "Test Hall", AddressID = AddressId, IsPublished = published, IsModerationBlocked = blocked, LogoPicID = logoPicId },
            ownerUserId,
            StaffId);
        return options;
    }

    private static VenueUpdateRequest Update(Action<VenueUpdateRequest>? tweak = null)
    {
        var request = new VenueUpdateRequest { VenueID = 1, Name = "Test Hall", AddressID = AddressId };
        tweak?.Invoke(request);
        return request;
    }

    private static void AssertForbidden(object? result)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    // ---- PUT /api/venue/{id}

    [Fact]
    public async Task PutVenue_ByNonOwner_Returns403AndChangesNothing()
    {
        var options = await SeedHallAsync(nameof(PutVenue_ByNonOwner_Returns403AndChangesNothing));
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OtherUserId).PutVenue(1, Update(r => r.Name = "Hijacked"));

        AssertForbidden(result);
        await using var verify = new TAGDBContext(options);
        Assert.Equal("Test Hall", (await verify.Set<Venue>().SingleAsync()).Name);
    }

    [Fact]
    public async Task PutVenue_ByOwner_Succeeds()
    {
        var options = await SeedHallAsync(nameof(PutVenue_ByOwner_Succeeds));
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OwnerId).PutVenue(1, Update(r => r.Name = "Renamed Hall"));

        Assert.IsType<NoContentResult>(result);
        await using var verify = new TAGDBContext(options);
        Assert.Equal("Renamed Hall", (await verify.Set<Venue>().SingleAsync()).Name);
    }

    [Fact]
    public async Task PutVenue_ByStaff_CanEditAnUnownedVenue()
    {
        var options = await SeedHallAsync(nameof(PutVenue_ByStaff_CanEditAnUnownedVenue), ownerUserId: null);
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, StaffId).PutVenue(1, Update(r => r.Name = "Staff Edit"));

        Assert.IsType<NoContentResult>(result);
        await using var verify = new TAGDBContext(options);
        Assert.Equal("Staff Edit", (await verify.Set<Venue>().SingleAsync()).Name);
    }

    [Fact]
    public async Task PutVenue_ByNonStaff_OnAnUnownedVenue_Returns403()
    {
        var options = await SeedHallAsync(nameof(PutVenue_ByNonStaff_OnAnUnownedVenue_Returns403), ownerUserId: null);
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OtherUserId).PutVenue(1, Update(r => r.Name = "Nope"));

        AssertForbidden(result);
    }

    [Fact]
    public async Task PutVenue_Anonymous_Returns401()
    {
        var options = await SeedHallAsync(nameof(PutVenue_Anonymous_Returns401));
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, userId: null).PutVenue(1, Update());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task PutVenue_UnknownVenue_Returns404()
    {
        var options = await SeedHallAsync(nameof(PutVenue_UnknownVenue_Returns404));
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OwnerId).PutVenue(77, Update(r => r.VenueID = 77));

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PutVenue_NonOwnerWithAnInvalidBody_GetsForbiddenNotValidationFeedback()
    {
        var options = await SeedHallAsync(nameof(PutVenue_NonOwnerWithAnInvalidBody_GetsForbiddenNotValidationFeedback));
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OtherUserId).PutVenue(1, Update(r => r.Name = " "));

        AssertForbidden(result);
    }

    [Fact]
    public async Task PutVenue_ByOwner_CannotChangeTheModerationFlag()
    {
        var options = await SeedHallAsync(nameof(PutVenue_ByOwner_CannotChangeTheModerationFlag));
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OwnerId).PutVenue(1, Update(r => { r.Name = "Sneaky"; r.IsModerationBlocked = true; }));

        AssertForbidden(result);
        await using var verify = new TAGDBContext(options);
        var stored = await verify.Set<Venue>().SingleAsync();
        Assert.False(stored.IsModerationBlocked);
        Assert.Equal("Test Hall", stored.Name);
    }

    [Fact]
    public async Task PutVenue_ByOwnerOfABlockedVenue_CannotUnblockIt()
    {
        var options = await SeedHallAsync(nameof(PutVenue_ByOwnerOfABlockedVenue_CannotUnblockIt), blocked: true);
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OwnerId).PutVenue(1, Update(r => r.IsModerationBlocked = false));

        AssertForbidden(result);
        await using var verify = new TAGDBContext(options);
        Assert.True((await verify.Set<Venue>().SingleAsync()).IsModerationBlocked);
    }

    [Fact]
    public async Task PutVenue_ByOwner_SendingTheUnchangedModerationFlagIsAllowed()
    {
        var options = await SeedHallAsync(nameof(PutVenue_ByOwner_SendingTheUnchangedModerationFlagIsAllowed));
        await using var context = new TAGDBContext(options);

        // The edit forms send back the value they loaded.
        var result = await CreateController(context, OwnerId).PutVenue(1, Update(r => { r.Name = "Renamed"; r.IsModerationBlocked = false; }));

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task PutVenue_ByStaff_CanBlockAndUnblock()
    {
        var options = await SeedHallAsync(nameof(PutVenue_ByStaff_CanBlockAndUnblock));
        await using var context = new TAGDBContext(options);
        var controller = CreateController(context, StaffId);

        Assert.IsType<NoContentResult>(await controller.PutVenue(1, Update(r => r.IsModerationBlocked = true)));
        await using (var blocked = new TAGDBContext(options))
        {
            Assert.True((await blocked.Set<Venue>().SingleAsync()).IsModerationBlocked);
        }

        context.ChangeTracker.Clear();
        Assert.IsType<NoContentResult>(await controller.PutVenue(1, Update(r => r.IsModerationBlocked = false)));
        await using var unblocked = new TAGDBContext(options);
        Assert.False((await unblocked.Set<Venue>().SingleAsync()).IsModerationBlocked);
    }

    [Fact]
    public async Task PutVenue_ByOwner_CanPublishWithoutApproval()
    {
        var options = await SeedHallAsync(nameof(PutVenue_ByOwner_CanPublishWithoutApproval));
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OwnerId).PutVenue(1, Update(r => r.IsPublished = true));

        Assert.IsType<NoContentResult>(result);
        await using var verify = new TAGDBContext(options);
        Assert.True((await verify.Set<Venue>().SingleAsync()).IsPublished);
    }

    [Fact]
    public async Task PutVenue_WithoutAnIsPublishedValue_LeavesThePublishedStateUnchanged()
    {
        var options = await SeedHallAsync(nameof(PutVenue_WithoutAnIsPublishedValue_LeavesThePublishedStateUnchanged), published: true);
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OwnerId).PutVenue(1, Update(r => r.Name = "Renamed"));

        Assert.IsType<NoContentResult>(result);
        await using var verify = new TAGDBContext(options);
        Assert.True((await verify.Set<Venue>().SingleAsync()).IsPublished);
    }

    [Fact]
    public async Task PutVenue_DoesNotOverwriteTheLogoOrTheOwnerLinks()
    {
        var options = await SeedHallAsync(nameof(PutVenue_DoesNotOverwriteTheLogoOrTheOwnerLinks), logoPicId: 5);
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OwnerId).PutVenue(1, Update(r => r.Name = "Renamed"));

        Assert.IsType<NoContentResult>(result);
        await using var verify = new TAGDBContext(options);
        Assert.Equal(5, (await verify.Set<Venue>().SingleAsync()).LogoPicID);
        Assert.Single(verify.Set<Linker_UserToVenue>());
    }

    // ---- DELETE /api/venue/{id}

    [Fact]
    public async Task DeleteVenue_ByNonOwner_Returns403AndKeepsTheVenue()
    {
        var options = await SeedHallAsync(nameof(DeleteVenue_ByNonOwner_Returns403AndKeepsTheVenue));
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OtherUserId).DeleteVenue(1);

        AssertForbidden(result);
        await using var verify = new TAGDBContext(options);
        Assert.Single(verify.Set<Venue>());
    }

    [Theory]
    [InlineData(OwnerId)]
    [InlineData(StaffId)]
    public async Task DeleteVenue_ByOwnerOrStaff_DeletesTheVenue(int userId)
    {
        var options = await SeedHallAsync($"{nameof(DeleteVenue_ByOwnerOrStaff_DeletesTheVenue)}-{userId}");
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, userId).DeleteVenue(1);

        Assert.IsType<NoContentResult>(result);
        await using var verify = new TAGDBContext(options);
        Assert.Empty(verify.Set<Venue>());
    }

    [Fact]
    public async Task DeleteVenue_AnonymousOrUnknown_Returns401Or404()
    {
        var options = await SeedHallAsync(nameof(DeleteVenue_AnonymousOrUnknown_Returns401Or404));
        await using var context = new TAGDBContext(options);

        Assert.IsType<UnauthorizedResult>(await CreateController(context, userId: null).DeleteVenue(1));
        Assert.IsType<NotFoundResult>(await CreateController(context, OwnerId).DeleteVenue(77));
        Assert.Single(context.Set<Venue>());
    }

    // ---- PATCH /api/venue/{id}/update-slug

    [Fact]
    public async Task UpdateVenueSlug_ByNonOwner_Returns403AndKeepsTheName()
    {
        var options = await SeedHallAsync(nameof(UpdateVenueSlug_ByNonOwner_Returns403AndKeepsTheName));
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OtherUserId).UpdateVenueSlug(1, new VenueSlugUpdateRequest { Slug = "stolen", Title = "Stolen" });

        AssertForbidden(result.Result);
        await using var verify = new TAGDBContext(options);
        Assert.Equal("Test Hall", (await verify.Set<Venue>().SingleAsync()).Name);
    }

    [Fact]
    public async Task UpdateVenueSlug_ByOwner_Renames()
    {
        var options = await SeedHallAsync(nameof(UpdateVenueSlug_ByOwner_Renames));
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OwnerId).UpdateVenueSlug(1, new VenueSlugUpdateRequest { Slug = "renamed-hall", Title = "Renamed Hall" });

        Assert.IsType<OkObjectResult>(result.Result);
        await using var verify = new TAGDBContext(options);
        Assert.Equal("Renamed Hall", (await verify.Set<Venue>().SingleAsync()).Name);
    }

    // ---- creating a venue links the creator as its owner

    [Fact]
    public async Task ReserveVenueSlug_LinksTheCreatorAsOwnerAndStartsUnpublished()
    {
        var options = CreateOptions(nameof(ReserveVenueSlug_LinksTheCreatorAsOwnerAndStartsUnpublished));
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OwnerId).ReserveVenueSlug(new VenueSlugReservationRequest { Slug = "fresh-venue", Title = "Fresh Venue" });

        Assert.IsType<CreatedAtActionResult>(result.Result);
        var venue = await context.Set<Venue>().SingleAsync();
        Assert.False(venue.IsPublished);
        var link = await context.Set<Linker_UserToVenue>().SingleAsync();
        Assert.Equal(OwnerId, link.UserID);
        Assert.Equal(venue.VenueID, link.VenueID);
        Assert.Equal(Linker_UserToVenue.OwnerRole, link.Role);

        // The address is created together with the venue and the two are connected.
        var address = await context.Set<Address>().SingleAsync();
        Assert.Equal(address.AddressID, venue.AddressID);
        Assert.Equal("Pending venue address", address.AddressLine1);
    }

    [Fact]
    public async Task ReserveVenueSlug_Anonymous_Returns401AndCreatesNothing()
    {
        var options = CreateOptions(nameof(ReserveVenueSlug_Anonymous_Returns401AndCreatesNothing));
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, userId: null).ReserveVenueSlug(new VenueSlugReservationRequest { Slug = "fresh-venue", Title = "Fresh Venue" });

        Assert.IsType<UnauthorizedResult>(result.Result);
        Assert.Empty(context.Set<Venue>());
        Assert.Empty(context.Set<Linker_UserToVenue>());
    }

    [Fact]
    public async Task PostVenue_LinksTheCreatorAsOwnerAndStartsUnpublished()
    {
        var options = CreateOptions(nameof(PostVenue_LinksTheCreatorAsOwnerAndStartsUnpublished));
        await SeedAsync(options);
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OwnerId).PostVenue(new VenueCreateRequest { Name = "New Hall", AddressID = AddressId });

        Assert.IsType<CreatedAtActionResult>(result.Result);
        var venue = await context.Set<Venue>().SingleAsync();
        Assert.False(venue.IsPublished);
        Assert.False(venue.IsModerationBlocked);
        Assert.Equal(OwnerId, (await context.Set<Linker_UserToVenue>().SingleAsync()).UserID);
    }

    [Fact]
    public async Task PostVenue_Anonymous_Returns401()
    {
        var options = CreateOptions(nameof(PostVenue_Anonymous_Returns401));
        await SeedAsync(options);
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, userId: null).PostVenue(new VenueCreateRequest { Name = "New Hall", AddressID = AddressId });

        Assert.IsType<UnauthorizedResult>(result.Result);
        Assert.Empty(context.Set<Venue>());
    }

    // ---- reading: drafts and blocked venues are private

    [Fact]
    public async Task GetVenueById_Draft_IsHiddenFromOthersButVisibleToTheOwnerAndStaff()
    {
        var options = await SeedHallAsync(nameof(GetVenueById_Draft_IsHiddenFromOthersButVisibleToTheOwnerAndStaff));
        await using var context = new TAGDBContext(options);

        Assert.IsType<NotFoundResult>((await CreateController(context, OtherUserId).GetVenueById(1)).Result);
        Assert.IsType<NotFoundResult>((await CreateController(context, userId: null).GetVenueById(1)).Result);
        Assert.IsType<OkObjectResult>((await CreateController(context, OwnerId).GetVenueById(1)).Result);
        Assert.IsType<OkObjectResult>((await CreateController(context, StaffId).GetVenueById(1)).Result);
    }

    [Fact]
    public async Task GetVenueById_BlockedVenue_IsHiddenFromOthersEvenWhenPublished()
    {
        var options = await SeedHallAsync(nameof(GetVenueById_BlockedVenue_IsHiddenFromOthersEvenWhenPublished), published: true, blocked: true);
        await using var context = new TAGDBContext(options);

        Assert.IsType<NotFoundResult>((await CreateController(context, OtherUserId).GetVenueById(1)).Result);
        Assert.IsType<OkObjectResult>((await CreateController(context, OwnerId).GetVenueById(1)).Result);
    }

    [Fact]
    public async Task GetVenueById_Published_IsVisibleToAnySignedInUser()
    {
        var options = await SeedHallAsync(nameof(GetVenueById_Published_IsVisibleToAnySignedInUser), published: true);
        await using var context = new TAGDBContext(options);

        Assert.IsType<OkObjectResult>((await CreateController(context, OtherUserId).GetVenueById(1)).Result);
    }

    [Fact]
    public async Task GetVenueBySlug_Draft_IsHiddenFromOthersButVisibleToTheOwner()
    {
        var options = await SeedHallAsync(nameof(GetVenueBySlug_Draft_IsHiddenFromOthersButVisibleToTheOwner));
        await using var context = new TAGDBContext(options);

        Assert.IsType<NotFoundResult>((await CreateController(context, OtherUserId).GetVenueBySlug("test-hall")).Result);
        var owner = Assert.IsType<OkObjectResult>((await CreateController(context, OwnerId).GetVenueBySlug("test-hall")).Result);
        Assert.Equal("test-hall", Prop(owner.Value, "Slug"));
    }

    [Fact]
    public async Task GetUnpublishedVenues_IsStaffOnlyAndUsesTheTokenNotAQueryValue()
    {
        var options = await SeedHallAsync(nameof(GetUnpublishedVenues_IsStaffOnlyAndUsesTheTokenNotAQueryValue));
        await using var context = new TAGDBContext(options);

        Assert.IsType<UnauthorizedResult>((await CreateController(context, userId: null).GetUnpublishedVenues()).Result);
        AssertForbidden((await CreateController(context, OtherUserId).GetUnpublishedVenues()).Result);
        AssertForbidden((await CreateController(context, OwnerId).GetUnpublishedVenues()).Result);

        var staff = Assert.IsType<OkObjectResult>((await CreateController(context, StaffId).GetUnpublishedVenues()).Result);
        Assert.Single(Assert.IsAssignableFrom<IEnumerable<object>>(staff.Value));
    }

    // ---- GET /api/Linker_UserToVenue/byUserID/{userId}

    [Fact]
    public async Task GetVenuesByUserId_ReturnsTheUsersOwnVenuesIncludingDraftsWithASlug()
    {
        var options = await SeedHallAsync(nameof(GetVenuesByUserId_ReturnsTheUsersOwnVenuesIncludingDraftsWithASlug));
        await using (var seed = new TAGDBContext(options))
        {
            seed.Set<Venue>().Add(new Venue { VenueID = 2, Name = "Second Stage", AddressID = AddressId, IsPublished = true });
            seed.Set<Venue>().Add(new Venue { VenueID = 3, Name = "Not Mine", AddressID = AddressId, IsPublished = true });
            seed.Set<Linker_UserToVenue>().Add(new Linker_UserToVenue { UserID = OwnerId, VenueID = 2 });
            seed.Set<Linker_UserToVenue>().Add(new Linker_UserToVenue { UserID = OtherUserId, VenueID = 3 });
            await seed.SaveChangesAsync();
        }

        await using var context = new TAGDBContext(options);
        var result = await CreateLinkerController(context, OwnerId).GetVenuesByUserId(OwnerId);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var venues = Assert.IsAssignableFrom<IEnumerable<object>>(ok.Value).ToList();
        Assert.Equal(new[] { "Second Stage", "Test Hall" }, venues.Select(v => (string?)Prop(v, "Name")).ToArray());
        Assert.Equal(new[] { "second-stage", "test-hall" }, venues.Select(v => (string?)Prop(v, "Slug")).ToArray());
    }

    [Fact]
    public async Task GetVenuesByUserId_AnotherUsersList_IsForbiddenButStaffMayRead()
    {
        var options = await SeedHallAsync(nameof(GetVenuesByUserId_AnotherUsersList_IsForbiddenButStaffMayRead));
        await using var context = new TAGDBContext(options);

        AssertForbidden((await CreateLinkerController(context, OtherUserId).GetVenuesByUserId(OwnerId)).Result);

        var staff = Assert.IsType<OkObjectResult>((await CreateLinkerController(context, StaffId).GetVenuesByUserId(OwnerId)).Result);
        Assert.Single(Assert.IsAssignableFrom<IEnumerable<object>>(staff.Value));
    }

    [Fact]
    public async Task GetVenuesByUserId_WithNoLinks_ReturnsAnEmptyList_AndAnonymousGets401()
    {
        var options = await SeedHallAsync(nameof(GetVenuesByUserId_WithNoLinks_ReturnsAnEmptyList_AndAnonymousGets401));
        await using var context = new TAGDBContext(options);

        var empty = Assert.IsType<OkObjectResult>((await CreateLinkerController(context, OtherUserId).GetVenuesByUserId(OtherUserId)).Result);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<object>>(empty.Value));

        Assert.IsType<UnauthorizedResult>((await CreateLinkerController(context, userId: null).GetVenuesByUserId(OwnerId)).Result);
    }
}
