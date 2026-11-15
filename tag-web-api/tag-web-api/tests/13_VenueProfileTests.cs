using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Controllers;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;
using Xunit;
using static tagApiUnitTests.VenueTestSupport;

namespace tagApiUnitTests;

// Stories #330 and #331: the venue address, who may edit a venue, and the rule that a venue needs a real address
// before it is published. Database names start with "venueprofile-" because they are shared across test classes.
public class VenueProfileTests
{
    private const int OwnerId = 10;
    private const int OtherUserId = 20;
    private const int StaffId = 99;

    private static async Task<DbContextOptions<TAGDBContext>> SeedAsync(string name, bool published = false, int? ownerUserId = OwnerId, bool pendingAddress = false)
    {
        var options = CreateOptions($"venueprofile-{name}");
        await VenueTestSupport.SeedAsync(
            options,
            new Venue { VenueID = 1, Name = "Test Hall", AddressID = AddressId, IsPublished = published },
            ownerUserId,
            StaffId);

        if (pendingAddress)
        {
            await using var context = new TAGDBContext(options);
            var address = await context.Set<Address>().SingleAsync();
            address.AddressLine1 = "Pending venue address";
            address.City = "Pending";
            await context.SaveChangesAsync();
        }

        return options;
    }

    private static VenueAddressRequest AddressBody(Action<VenueAddressRequest>? tweak = null)
    {
        var request = new VenueAddressRequest { AddressLine1 = "12 Peach St", AddressLine2 = "Suite 4", City = "Savannah", State = "ga", ZipCode = "31401" };
        tweak?.Invoke(request);
        return request;
    }

    private static void AssertStatus(object? result, int status)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(status, objectResult.StatusCode);
    }

    // ---- PUT /api/venue/{id}/address

    [Fact]
    public async Task PutAddress_ByOwner_SavesTheAddressAndNormalizesTheState()
    {
        var options = await SeedAsync(nameof(PutAddress_ByOwner_SavesTheAddressAndNormalizesTheState));
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OwnerId).PutVenueAddress(1, AddressBody(r => r.City = "  Savannah  "));

        Assert.IsType<NoContentResult>(result);
        await using var verify = new TAGDBContext(options);
        var address = await verify.Set<Address>().SingleAsync();
        Assert.Equal("12 Peach St", address.AddressLine1);
        Assert.Equal("Suite 4", address.AddressLine2);
        Assert.Equal("Savannah", address.City);
        Assert.Equal("GA", address.State);
        Assert.Equal("31401", address.ZipCode);
        Assert.Equal("USA", address.Country);
        Assert.Equal(AddressId, (await verify.Set<Venue>().SingleAsync()).AddressID);
    }

    [Theory]
    [InlineData("Georgia", "GA")]
    [InlineData("north carolina", "NC")]
    [InlineData("DC", "DC")]
    public async Task PutAddress_AcceptsAStateCodeOrName(string state, string expectedCode)
    {
        var options = await SeedAsync($"{nameof(PutAddress_AcceptsAStateCodeOrName)}-{expectedCode}");
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OwnerId).PutVenueAddress(1, AddressBody(r => r.State = state));

        Assert.IsType<NoContentResult>(result);
        await using var verify = new TAGDBContext(options);
        Assert.Equal(expectedCode, (await verify.Set<Address>().SingleAsync()).State);
    }

    [Fact]
    public async Task PutAddress_ByStaff_CanEditAnUnownedVenue()
    {
        var options = await SeedAsync(nameof(PutAddress_ByStaff_CanEditAnUnownedVenue), ownerUserId: null);
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, StaffId).PutVenueAddress(1, AddressBody());

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task PutAddress_ByNonOwner_Returns403AndChangesNothing()
    {
        var options = await SeedAsync(nameof(PutAddress_ByNonOwner_Returns403AndChangesNothing));
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OtherUserId).PutVenueAddress(1, AddressBody());

        AssertStatus(result, StatusCodes.Status403Forbidden);
        await using var verify = new TAGDBContext(options);
        Assert.Equal("1 Main St", (await verify.Set<Address>().SingleAsync()).AddressLine1);
    }

    [Fact]
    public async Task PutAddress_Anonymous_Returns401()
    {
        var options = await SeedAsync(nameof(PutAddress_Anonymous_Returns401));
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, userId: null).PutVenueAddress(1, AddressBody());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task PutAddress_ForAMissingVenue_Returns404()
    {
        var options = await SeedAsync(nameof(PutAddress_ForAMissingVenue_Returns404));
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OwnerId).PutVenueAddress(404, AddressBody());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PutAddress_PermissionIsCheckedBeforeTheBodyIsValidated()
    {
        var options = await SeedAsync(nameof(PutAddress_PermissionIsCheckedBeforeTheBodyIsValidated));
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OtherUserId).PutVenueAddress(1, AddressBody(r => r.City = null));

        AssertStatus(result, StatusCodes.Status403Forbidden);
    }

    [Theory]
    [InlineData("AddressLine1", null)]
    [InlineData("AddressLine1", "   ")]
    [InlineData("City", "")]
    [InlineData("State", "")]
    [InlineData("State", "Atlantis")]
    [InlineData("State", "G")]
    [InlineData("ZipCode", "")]
    [InlineData("ZipCode", "3030")]
    [InlineData("ZipCode", "30301-12")]
    [InlineData("ZipCode", "ABCDE")]
    public async Task PutAddress_RejectsInvalidFieldsAndChangesNothing(string field, string? value)
    {
        var options = await SeedAsync($"{nameof(PutAddress_RejectsInvalidFieldsAndChangesNothing)}-{field}-{value}");
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OwnerId).PutVenueAddress(1, AddressBody(r => typeof(VenueAddressRequest).GetProperty(field)!.SetValue(r, value)));

        AssertStatus(result, StatusCodes.Status400BadRequest);
        await using var verify = new TAGDBContext(options);
        Assert.Equal("1 Main St", (await verify.Set<Address>().SingleAsync()).AddressLine1);
    }

    [Fact]
    public async Task PutAddress_RejectsTooLongValues()
    {
        var options = await SeedAsync(nameof(PutAddress_RejectsTooLongValues));
        await using var context = new TAGDBContext(options);
        var controller = CreateController(context, OwnerId);

        AssertStatus(await controller.PutVenueAddress(1, AddressBody(r => r.AddressLine1 = new string('a', 256))), StatusCodes.Status400BadRequest);
        AssertStatus(await controller.PutVenueAddress(1, AddressBody(r => r.City = new string('c', 101))), StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task PutAddress_AcceptsAZipPlusFour_AndDropsAnEmptySecondLine()
    {
        var options = await SeedAsync(nameof(PutAddress_AcceptsAZipPlusFour_AndDropsAnEmptySecondLine));
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OwnerId).PutVenueAddress(1, AddressBody(r => { r.ZipCode = "31401-1234"; r.AddressLine2 = "  "; }));

        Assert.IsType<NoContentResult>(result);
        await using var verify = new TAGDBContext(options);
        var address = await verify.Set<Address>().SingleAsync();
        Assert.Equal("31401-1234", address.ZipCode);
        Assert.Null(address.AddressLine2);
    }

    [Fact]
    public async Task PutAddress_WhenAnotherVenueSharesTheAddress_GivesThisVenueANewRowAndLeavesTheOtherAlone()
    {
        var options = await SeedAsync(nameof(PutAddress_WhenAnotherVenueSharesTheAddress_GivesThisVenueANewRowAndLeavesTheOtherAlone));
        await using (var seed = new TAGDBContext(options))
        {
            seed.Set<Venue>().Add(new Venue { VenueID = 2, Name = "Other Hall", AddressID = AddressId });
            await seed.SaveChangesAsync();
        }

        await using var context = new TAGDBContext(options);
        var result = await CreateController(context, OwnerId).PutVenueAddress(1, AddressBody());

        Assert.IsType<NoContentResult>(result);
        await using var verify = new TAGDBContext(options);
        var mine = await verify.Set<Venue>().Include(v => v.Address).SingleAsync(v => v.VenueID == 1);
        var other = await verify.Set<Venue>().Include(v => v.Address).SingleAsync(v => v.VenueID == 2);
        Assert.NotEqual(AddressId, mine.AddressID);
        Assert.Equal("12 Peach St", mine.Address!.AddressLine1);
        Assert.Equal(AddressId, other.AddressID);
        Assert.Equal("1 Main St", other.Address!.AddressLine1);
    }

    // ---- GET /api/venue/{id}/access

    [Fact]
    public async Task GetAccess_ForAnOwner_CanManage()
    {
        var options = await SeedAsync(nameof(GetAccess_ForAnOwner_CanManage));
        await using var context = new TAGDBContext(options);

        var result = Assert.IsType<OkObjectResult>(await CreateController(context, OwnerId).GetVenueAccess(1));

        Assert.Equal(true, Prop(result.Value, "canManage"));
        Assert.Equal(true, Prop(result.Value, "isOwner"));
        Assert.Equal(false, Prop(result.Value, "isStaff"));
    }

    [Fact]
    public async Task GetAccess_ForStaff_CanManageAnUnownedVenue()
    {
        var options = await SeedAsync(nameof(GetAccess_ForStaff_CanManageAnUnownedVenue), ownerUserId: null);
        await using var context = new TAGDBContext(options);

        var result = Assert.IsType<OkObjectResult>(await CreateController(context, StaffId).GetVenueAccess(1));

        Assert.Equal(true, Prop(result.Value, "canManage"));
        Assert.Equal(true, Prop(result.Value, "isStaff"));
    }

    [Fact]
    public async Task GetAccess_ForSomeoneElse_CannotManage_EvenWhenTheVenueIsPublished()
    {
        var options = await SeedAsync(nameof(GetAccess_ForSomeoneElse_CannotManage_EvenWhenTheVenueIsPublished), published: true);
        await using var context = new TAGDBContext(options);

        var result = Assert.IsType<OkObjectResult>(await CreateController(context, OtherUserId).GetVenueAccess(1));

        Assert.Equal(false, Prop(result.Value, "canManage"));
    }

    [Fact]
    public async Task GetAccess_Anonymous_Returns401_AndAMissingVenueReturns404()
    {
        var options = await SeedAsync(nameof(GetAccess_Anonymous_Returns401_AndAMissingVenueReturns404));
        await using var context = new TAGDBContext(options);

        Assert.IsType<UnauthorizedResult>(await CreateController(context, userId: null).GetVenueAccess(1));
        Assert.IsType<NotFoundResult>(await CreateController(context, OwnerId).GetVenueAccess(404));
    }

    // ---- the venue as the edit form loads it

    [Fact]
    public async Task GetVenueById_IncludesTheAddress_ForTheOwnerOfADraft()
    {
        var options = await SeedAsync(nameof(GetVenueById_IncludesTheAddress_ForTheOwnerOfADraft));
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OwnerId).GetVenueById(1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var address = Prop(ok.Value, "Address") as Address;
        Assert.NotNull(address);
        Assert.Equal("1 Main St", address!.AddressLine1);
        Assert.Equal("Atlanta", address.City);
    }

    // ---- publishing needs a real address

    [Fact]
    public async Task Publish_WithThePlaceholderAddress_Returns400_AndStaysUnpublished()
    {
        var options = await SeedAsync(nameof(Publish_WithThePlaceholderAddress_Returns400_AndStaysUnpublished), pendingAddress: true);
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OwnerId).PutVenue(1, new VenueUpdateRequest { VenueID = 1, Name = "Test Hall", AddressID = AddressId, IsPublished = true });

        AssertStatus(result, StatusCodes.Status400BadRequest);
        await using var verify = new TAGDBContext(options);
        Assert.False((await verify.Set<Venue>().SingleAsync()).IsPublished);
    }

    [Fact]
    public async Task Publish_AfterTheAddressIsSaved_Succeeds()
    {
        var options = await SeedAsync(nameof(Publish_AfterTheAddressIsSaved_Succeeds), pendingAddress: true);
        await using (var context = new TAGDBContext(options))
        {
            Assert.IsType<NoContentResult>(await CreateController(context, OwnerId).PutVenueAddress(1, AddressBody()));
        }

        await using var publishContext = new TAGDBContext(options);
        var result = await CreateController(publishContext, OwnerId).PutVenue(1, new VenueUpdateRequest { VenueID = 1, Name = "Test Hall", AddressID = AddressId, IsPublished = true });

        Assert.IsType<NoContentResult>(result);
        await using var verify = new TAGDBContext(options);
        Assert.True((await verify.Set<Venue>().SingleAsync()).IsPublished);
    }

    [Fact]
    public async Task SavingAnAlreadyPublishedVenue_WithThePlaceholderAddress_IsNotBlocked()
    {
        var options = await SeedAsync(nameof(SavingAnAlreadyPublishedVenue_WithThePlaceholderAddress_IsNotBlocked), published: true, pendingAddress: true);
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OwnerId).PutVenue(1, new VenueUpdateRequest { VenueID = 1, Name = "Renamed Hall", AddressID = AddressId, IsPublished = true });

        Assert.IsType<NoContentResult>(result);
    }
}
