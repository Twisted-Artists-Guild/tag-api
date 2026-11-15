using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Controllers;
using TAGWEBAPI.Controllers.Contact;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;
using Xunit;
using static tagApiUnitTests.VenueTestSupport;

namespace tagApiUnitTests;

// Story #340, security review: contacts, contact links and addresses are shared by venues, artists and users, but the ones that
// belong to a venue follow the venue's rules. Private ones are for the venue's owners and staff, and only they can change them.
// Database names start with "venuedata-" because they are shared across test classes.
public class VenueDataSecurityTests
{
    private const int StaffId = 1;
    private const int OwnerId = 10; // owns the public venue
    private const int DraftOwnerId = 11; // owns the draft venue
    private const int StrangerId = 30;

    private const int PublicVenue = 1;
    private const int DraftVenue = 2;
    private const int BlockedVenue = 3;

    // Addresses
    private const int PublicVenueAddress = 901;
    private const int DraftVenueAddress = 902;
    private const int FreeAddress = 904;
    private const int PrivateContactAddress = 905;
    private const int PublicContactAddress = 906;
    private const int SharedAddress = 907;

    // Contacts
    private const int PublicContact = 1;
    private const int PrivateContact = 2;
    private const int DraftVenueContact = 3;
    private const int ArtistContact = 4;

    private static Address NewAddress(int id) => new() { AddressID = id, AddressLine1 = $"{id} Main St", City = "Atlanta", State = "GA", ZipCode = "30301", Country = "USA" };

    private static async Task<DbContextOptions<TAGDBContext>> SeedAsync(string name)
    {
        var options = CreateOptions($"venuedata-{name}");
        await using var seed = new TAGDBContext(options);
        seed.Users.Add(new User { UserID = StaffId, EmailOne = "staff@example.com", Username = "staff", Moderator = true });
        seed.Set<Address>().AddRange(new[] { PublicVenueAddress, DraftVenueAddress, 903, FreeAddress, PrivateContactAddress, PublicContactAddress, SharedAddress }.Select(NewAddress));
        seed.Set<Venue>().AddRange(
            new Venue { VenueID = PublicVenue, Name = "Public Hall", AddressID = PublicVenueAddress, IsPublished = true },
            new Venue { VenueID = DraftVenue, Name = "Draft Hall", AddressID = DraftVenueAddress, IsPublished = false },
            new Venue { VenueID = BlockedVenue, Name = "Blocked Hall", AddressID = 903, IsPublished = true, IsModerationBlocked = true },
            new Venue { VenueID = 4, Name = "Sharing Hall", AddressID = SharedAddress, IsPublished = true },
            new Venue { VenueID = 5, Name = "Also Sharing Hall", AddressID = SharedAddress, IsPublished = true });
        seed.Set<Linker_UserToVenue>().AddRange(
            new Linker_UserToVenue { UserID = OwnerId, VenueID = PublicVenue, Role = Linker_UserToVenue.OwnerRole },
            new Linker_UserToVenue { UserID = OwnerId, VenueID = 4, Role = Linker_UserToVenue.OwnerRole },
            new Linker_UserToVenue { UserID = DraftOwnerId, VenueID = DraftVenue, Role = Linker_UserToVenue.OwnerRole },
            new Linker_UserToVenue { UserID = DraftOwnerId, VenueID = 5, Role = Linker_UserToVenue.OwnerRole });

        seed.Set<Contact>().AddRange(
            new Contact { ContactID = PublicContact, ContactType = "url", Value = "https://public.example.com" },
            new Contact { ContactID = PrivateContact, ContactType = "url", Value = "https://private.example.com" },
            new Contact { ContactID = DraftVenueContact, ContactType = "url", Value = "https://draft.example.com" },
            new Contact { ContactID = ArtistContact, ContactType = "url", Value = "https://artist.example.com" },
            new Contact { ContactID = 5, ContactType = "address", AddressID = PrivateContactAddress },
            new Contact { ContactID = 6, ContactType = "address", AddressID = PublicContactAddress });
        seed.Set<Linker_EntityToContact>().AddRange(
            new Linker_EntityToContact { Linker_EntityToContactID = 1, ContactID = PublicContact, EntityType = LinkedEntityTypes.Venue, EntityID = PublicVenue, Scope = ContactScope.Primary },
            new Linker_EntityToContact { Linker_EntityToContactID = 2, ContactID = PrivateContact, EntityType = LinkedEntityTypes.Venue, EntityID = PublicVenue, Scope = ContactScope.Private },
            new Linker_EntityToContact { Linker_EntityToContactID = 3, ContactID = DraftVenueContact, EntityType = LinkedEntityTypes.Venue, EntityID = DraftVenue, Scope = ContactScope.Primary },
            new Linker_EntityToContact { Linker_EntityToContactID = 4, ContactID = ArtistContact, EntityType = LinkedEntityTypes.Artist, EntityID = 77, Scope = ContactScope.Primary },
            new Linker_EntityToContact { Linker_EntityToContactID = 5, ContactID = 5, EntityType = LinkedEntityTypes.Venue, EntityID = PublicVenue, Scope = ContactScope.Private },
            new Linker_EntityToContact { Linker_EntityToContactID = 6, ContactID = 6, EntityType = LinkedEntityTypes.Venue, EntityID = PublicVenue, Scope = ContactScope.Secondary });
        await seed.SaveChangesAsync();
        return options;
    }

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
        ActionResult<Address> { Result: not null } typed => Status(typed.Result),
        ObjectResult objectResult => objectResult.StatusCode ?? 200,
        StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
        _ => 200,
    };

    private static AddressController Addresses(TAGDBContext context, int? userId) => SignedIn(new AddressController(context), userId);

    private static ContactController Contacts(TAGDBContext context, int? userId) => SignedIn(new ContactController(context), userId);

    private static LinkerEntityToContactController Links(TAGDBContext context, int? userId) => SignedIn(new LinkerEntityToContactController(context), userId);

    // ---- addresses

    [Theory]
    [InlineData(null, 401)]
    [InlineData(StrangerId, 403)]
    [InlineData(OwnerId, 403)]
    [InlineData(StaffId, 200)]
    public async Task OnlyStaffCanListEveryAddress(int? userId, int expected)
    {
        var options = await SeedAsync($"{nameof(OnlyStaffCanListEveryAddress)}{userId}");
        await using var context = new TAGDBContext(options);

        Assert.Equal(expected, Status((await Addresses(context, userId).Get()).Result ?? new OkResult()));
    }

    [Theory]
    [InlineData(PublicVenueAddress, null, 200)]
    [InlineData(PublicVenueAddress, StrangerId, 200)]
    [InlineData(DraftVenueAddress, null, 404)]
    [InlineData(DraftVenueAddress, StrangerId, 404)]
    [InlineData(DraftVenueAddress, OwnerId, 404)]
    [InlineData(DraftVenueAddress, DraftOwnerId, 200)]
    [InlineData(DraftVenueAddress, StaffId, 200)]
    [InlineData(903, null, 404)]
    [InlineData(PrivateContactAddress, null, 404)]
    [InlineData(PrivateContactAddress, StrangerId, 404)]
    [InlineData(PrivateContactAddress, OwnerId, 200)]
    [InlineData(PublicContactAddress, null, 200)]
    [InlineData(FreeAddress, null, 200)]
    [InlineData(999, null, 404)]
    public async Task AnAddressOfAVenueIsReadableOnlyWhenTheVenueIsPublicOrTheCallerManagesIt(int addressId, int? userId, int expected)
    {
        var options = await SeedAsync($"{nameof(AnAddressOfAVenueIsReadableOnlyWhenTheVenueIsPublicOrTheCallerManagesIt)}{addressId}-{userId}");
        await using var context = new TAGDBContext(options);

        var result = await Addresses(context, userId).Get(addressId);

        Assert.Equal(expected, result.Result == null ? 200 : Status(result.Result));
    }

    [Theory]
    [InlineData(StrangerId, 403)]
    [InlineData(DraftOwnerId, 403)]
    [InlineData(OwnerId, 204)]
    [InlineData(StaffId, 204)]
    public async Task OnlyTheVenuesOwnersCanChangeItsAddress(int userId, int expected)
    {
        var options = await SeedAsync($"{nameof(OnlyTheVenuesOwnersCanChangeItsAddress)}{userId}");
        await using var context = new TAGDBContext(options);
        var edited = NewAddress(PublicVenueAddress);
        edited.City = "Savannah";

        var update = await Addresses(context, userId).Update(PublicVenueAddress, edited);

        Assert.Equal(expected, Status(update));
        await using var check = new TAGDBContext(options);
        Assert.Equal(expected == 204 ? "Savannah" : "Atlanta", (await check.Set<Address>().AsNoTracking().SingleAsync(a => a.AddressID == PublicVenueAddress)).City);
    }

    [Theory]
    [InlineData(StrangerId, 403)]
    [InlineData(OwnerId, 204)]
    public async Task OnlyTheVenuesOwnersCanDeleteItsAddress(int userId, int expected)
    {
        var options = await SeedAsync($"{nameof(OnlyTheVenuesOwnersCanDeleteItsAddress)}{userId}");
        await using var context = new TAGDBContext(options);

        Assert.Equal(expected, Status(await Addresses(context, userId).Delete(PublicVenueAddress)));
    }

    [Fact]
    public async Task AnAddressSharedByTwoVenues_NeedsBothOwnersOrStaff()
    {
        var options = await SeedAsync(nameof(AnAddressSharedByTwoVenues_NeedsBothOwnersOrStaff));
        await using (var context = new TAGDBContext(options))
        {
            Assert.Equal(403, Status(await Addresses(context, OwnerId).Update(SharedAddress, NewAddress(SharedAddress))));
        }

        await using (var context = new TAGDBContext(options))
        {
            Assert.Equal(403, Status(await Addresses(context, DraftOwnerId).Delete(SharedAddress)));
        }

        await using (var context = new TAGDBContext(options))
        {
            Assert.Equal(204, Status(await Addresses(context, StaffId).Update(SharedAddress, NewAddress(SharedAddress))));
        }
    }

    [Fact]
    public async Task AnAddressThatBelongsToNoVenue_IsChangedAsBefore()
    {
        var options = await SeedAsync(nameof(AnAddressThatBelongsToNoVenue_IsChangedAsBefore));
        await using var context = new TAGDBContext(options);

        Assert.Equal(204, Status(await Addresses(context, StrangerId).Update(FreeAddress, NewAddress(FreeAddress))));
    }

    // ---- venue contacts: reading

    private static async Task<List<object>> VenueContactsAsync(TAGDBContext context, int? userId, int venueId, bool includePrivate)
    {
        var result = await Contacts(context, userId).GetContactsByContext("venue", venueId, includePrivate);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return ((IEnumerable<object>)Prop(ok.Value, "contacts")!).ToList();
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData(StrangerId, 403)]
    [InlineData(DraftOwnerId, 403)]
    public async Task PrivateVenueContacts_AreRefusedToEveryoneButTheVenuesManagers(int? userId, int expected)
    {
        var options = await SeedAsync($"{nameof(PrivateVenueContacts_AreRefusedToEveryoneButTheVenuesManagers)}{userId}");
        await using var context = new TAGDBContext(options);

        var result = await Contacts(context, userId).GetContactsByContext("venue", PublicVenue, includePrivate: true);

        Assert.Equal(expected, Status(result.Result));
    }

    [Fact]
    public async Task TheOwnerAndStaff_SeePrivateVenueContacts_ButTheAnonymousListHidesThem()
    {
        var options = await SeedAsync(nameof(TheOwnerAndStaff_SeePrivateVenueContacts_ButTheAnonymousListHidesThem));
        await using var context = new TAGDBContext(options);

        Assert.Equal(4, (await VenueContactsAsync(context, OwnerId, PublicVenue, true)).Count);
        Assert.Equal(4, (await VenueContactsAsync(context, StaffId, PublicVenue, true)).Count);
        var publicRows = await VenueContactsAsync(context, null, PublicVenue, false);
        Assert.Equal(2, publicRows.Count);
        Assert.All(publicRows, row => Assert.Equal(false, Prop(row, "isPrivate")));
    }

    [Fact]
    public async Task TheContactsOfAVenueThatIsNotPublic_AreNotReadable()
    {
        var options = await SeedAsync(nameof(TheContactsOfAVenueThatIsNotPublic_AreNotReadable));
        await using var context = new TAGDBContext(options);

        Assert.IsType<NotFoundResult>((await Contacts(context, null).GetContactsByContext("venue", DraftVenue, false)).Result);
        Assert.Single(await VenueContactsAsync(context, DraftOwnerId, DraftVenue, true));
    }

    [Fact]
    public async Task ThePublicContactLists_LeaveOutPrivateContactsAndThoseOfNonPublicVenues()
    {
        var options = await SeedAsync(nameof(ThePublicContactLists_LeaveOutPrivateContactsAndThoseOfNonPublicVenues));
        await using var context = new TAGDBContext(options);
        var controller = Contacts(context, null);

        var listed = (await controller.GetContacts()).Value!.Select(c => c.ContactID).ToArray();

        Assert.Contains(PublicContact, listed);
        Assert.Contains(ArtistContact, listed);
        Assert.DoesNotContain(PrivateContact, listed);
        Assert.DoesNotContain(DraftVenueContact, listed);
        Assert.IsType<NotFoundResult>((await controller.GetContact(DraftVenueContact)).Result);
        Assert.NotNull((await controller.GetContact(PublicContact)).Value);
    }

    // ---- venue contacts: changing

    private static ManageContactRequest NewContact(int venueId) => new()
    {
        Context = "venue",
        EntityID = venueId,
        ContactType = "url",
        Value = "https://newsite.example.com",
    };

    [Theory]
    [InlineData(null, 401)]
    [InlineData(StrangerId, 403)]
    [InlineData(DraftOwnerId, 403)]
    [InlineData(OwnerId, 201)]
    [InlineData(StaffId, 201)]
    public async Task OnlyTheVenuesManagers_CanAddAContactToIt(int? userId, int expected)
    {
        var options = await SeedAsync($"{nameof(OnlyTheVenuesManagers_CanAddAContactToIt)}{userId}");
        await using var context = new TAGDBContext(options);

        var result = await Contacts(context, userId).PostManagedContact(NewContact(PublicVenue));

        Assert.Equal(expected, Status(result.Result));
        Assert.Equal(expected == 201 ? 3 : 2, await context.Set<Linker_EntityToContact>().CountAsync(l => l.EntityType == LinkedEntityTypes.Venue && l.EntityID == PublicVenue && l.ContactID != 5 && l.ContactID != 6));
    }

    [Fact]
    public async Task AddingAContactToAnArtist_IsNotChangedByTheVenueRules()
    {
        var options = await SeedAsync(nameof(AddingAContactToAnArtist_IsNotChangedByTheVenueRules));
        await using var context = new TAGDBContext(options);
        context.Artists.Add(new Artist { ArtistID = 77, Title = "Band", Path = "band", Applied = DateTime.UtcNow, Since = DateTime.UtcNow });
        await context.SaveChangesAsync();
        var request = NewContact(77);
        request.Context = "artist";

        Assert.Equal(201, Status((await Contacts(context, null).PostManagedContact(request)).Result));
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData(StrangerId, 403)]
    [InlineData(OwnerId, 204)]
    public async Task OnlyTheVenuesManagers_CanReorderItsContacts(int? userId, int expected)
    {
        var options = await SeedAsync($"{nameof(OnlyTheVenuesManagers_CanReorderItsContacts)}{userId}");
        await using var context = new TAGDBContext(options);

        var result = await Contacts(context, userId).PutContactOrder("venue", PublicVenue, new UpdateContactOrderRequest { LinkIds = new() { 2, 1 } });

        Assert.Equal(expected, Status(result));
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData(StrangerId, 403)]
    [InlineData(DraftOwnerId, 403)]
    [InlineData(OwnerId, 204)]
    [InlineData(StaffId, 204)]
    public async Task OnlyTheVenuesManagers_CanEditOrDeleteAVenueContact(int? userId, int expected)
    {
        var options = await SeedAsync($"{nameof(OnlyTheVenuesManagers_CanEditOrDeleteAVenueContact)}{userId}");
        await using var context = new TAGDBContext(options);
        var edited = new Contact { ContactID = PublicContact, ContactType = "url", Value = "https://changed.example.com" };

        Assert.Equal(expected, Status(await Contacts(context, userId).PutContact(PublicContact, edited)));
        Assert.Equal(expected, Status(await Contacts(context, userId).DeleteContact(PrivateContact)));
    }

    // ---- contact links

    private static async Task<int[]> LinkIdsAsync(TAGDBContext context, int? userId)
    {
        var listed = await Links(context, userId).GetLinks();
        return listed.Value!.Select(l => l.Linker_EntityToContactID).OrderBy(id => id).ToArray();
    }

    [Fact]
    public async Task TheLinkList_HidesPrivateVenueLinksAndThoseOfNonPublicVenues_FromTheAnonymous()
    {
        var options = await SeedAsync(nameof(TheLinkList_HidesPrivateVenueLinksAndThoseOfNonPublicVenues_FromTheAnonymous));
        await using var context = new TAGDBContext(options);

        Assert.Equal(new[] { 1, 4, 6 }, await LinkIdsAsync(context, null));
        Assert.Equal(new[] { 1, 4, 6 }, await LinkIdsAsync(context, StrangerId));
    }

    [Fact]
    public async Task TheLinkList_ShowsAnOwnerTheirOwnPrivateLinks_AndStaffEverything()
    {
        var options = await SeedAsync(nameof(TheLinkList_ShowsAnOwnerTheirOwnPrivateLinks_AndStaffEverything));
        await using var context = new TAGDBContext(options);

        Assert.Equal(new[] { 1, 2, 4, 5, 6 }, await LinkIdsAsync(context, OwnerId));
        Assert.Equal(new[] { 1, 3, 4, 6 }, await LinkIdsAsync(context, DraftOwnerId));
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, await LinkIdsAsync(context, StaffId));
    }

    [Fact]
    public async Task OnePrivateLink_AndTheLinksOfAVenue_FollowTheSameRules()
    {
        var options = await SeedAsync(nameof(OnePrivateLink_AndTheLinksOfAVenue_FollowTheSameRules));
        await using var context = new TAGDBContext(options);

        Assert.IsType<NotFoundResult>((await Links(context, null).GetLink(2)).Result);
        Assert.IsType<NotFoundResult>((await Links(context, StrangerId).GetLink(3)).Result);
        Assert.NotNull((await Links(context, OwnerId).GetLink(2)).Value);

        var anonymous = Assert.IsType<OkObjectResult>((await Links(context, null).GetLinksByEntity(LinkedEntityTypes.Venue, PublicVenue)).Result);
        Assert.Equal(new[] { 1, 6 }, ((IEnumerable<Linker_EntityToContact>)anonymous.Value!).Select(l => l.Linker_EntityToContactID).OrderBy(id => id).ToArray());
        var draft = Assert.IsType<OkObjectResult>((await Links(context, null).GetLinksByEntity(LinkedEntityTypes.Venue, DraftVenue)).Result);
        Assert.Empty((IEnumerable<Linker_EntityToContact>)draft.Value!);
    }

    [Fact]
    public async Task TheLinksOfAnArtist_AreNotChangedByTheVenueRules()
    {
        var options = await SeedAsync(nameof(TheLinksOfAnArtist_AreNotChangedByTheVenueRules));
        await using var context = new TAGDBContext(options);

        var artist = Assert.IsType<OkObjectResult>((await Links(context, null).GetLinksByEntity(LinkedEntityTypes.Artist, 77)).Result);

        Assert.Single((IEnumerable<Linker_EntityToContact>)artist.Value!);
    }

    private static LinkerCreateRequest NewLink(int venueId, int contactId) => new() { ContactID = contactId, EntityType = LinkedEntityTypes.Venue, EntityID = venueId, Scope = ContactScope.Secondary };

    [Theory]
    [InlineData(null, 401)]
    [InlineData(StrangerId, 403)]
    [InlineData(DraftOwnerId, 403)]
    [InlineData(OwnerId, 201)]
    [InlineData(StaffId, 201)]
    public async Task OnlyTheVenuesManagers_CanLinkAContactToIt(int? userId, int expected)
    {
        var options = await SeedAsync($"{nameof(OnlyTheVenuesManagers_CanLinkAContactToIt)}{userId}");
        await using (var seed = new TAGDBContext(options))
        {
            seed.Set<Contact>().Add(new Contact { ContactID = 50, ContactType = "url", Value = "https://fresh.example.com" });
            await seed.SaveChangesAsync();
        }

        await using var context = new TAGDBContext(options);

        Assert.Equal(expected, Status((await Links(context, userId).PostLink(NewLink(PublicVenue, 50))).Result));
    }

    [Fact]
    public async Task AContactThatBelongsToSomethingElse_CannotBeLinkedToAVenue()
    {
        var options = await SeedAsync(nameof(AContactThatBelongsToSomethingElse_CannotBeLinkedToAVenue));
        await using var context = new TAGDBContext(options);

        Assert.Equal(403, Status((await Links(context, OwnerId).PostLink(NewLink(PublicVenue, ArtistContact))).Result));
        Assert.Equal(403, Status((await Links(context, OwnerId).PostLink(NewLink(PublicVenue, DraftVenueContact))).Result));
        Assert.Equal(201, Status((await Links(context, StaffId).PostLink(NewLink(PublicVenue, ArtistContact))).Result));
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData(StrangerId, 403)]
    [InlineData(OwnerId, 204)]
    public async Task OnlyTheVenuesManagers_CanChangeOrRemoveAVenueLink(int? userId, int expected)
    {
        var options = await SeedAsync($"{nameof(OnlyTheVenuesManagers_CanChangeOrRemoveAVenueLink)}{userId}");
        await using var context = new TAGDBContext(options);
        var edited = new Linker_EntityToContact { Linker_EntityToContactID = 1, ContactID = PublicContact, EntityType = LinkedEntityTypes.Venue, EntityID = PublicVenue, Scope = ContactScope.Secondary, DisplayOrder = 3 };

        Assert.Equal(expected, Status(await Links(context, userId).PutLink(1, edited)));
        Assert.Equal(expected, Status(await Links(context, userId).DeleteLink(2)));
    }

    [Fact]
    public async Task ALinkCannotBeMovedToAVenueTheCallerDoesNotManage()
    {
        var options = await SeedAsync(nameof(ALinkCannotBeMovedToAVenueTheCallerDoesNotManage));
        await using var context = new TAGDBContext(options);
        var moved = new Linker_EntityToContact { Linker_EntityToContactID = 1, ContactID = PublicContact, EntityType = LinkedEntityTypes.Venue, EntityID = DraftVenue, Scope = ContactScope.Primary };

        Assert.Equal(403, Status(await Links(context, OwnerId).PutLink(1, moved)));
    }

    // ---- venue records pointing at someone else's rows

    private static VenueUpdateRequest Update(int venueId, int addressId) => new() { VenueID = venueId, Name = "Public Hall", AddressID = addressId };

    [Fact]
    public async Task AnOwner_CannotPointAVenueAtAnotherAddress_ButStaffCan()
    {
        var options = await SeedAsync(nameof(AnOwner_CannotPointAVenueAtAnotherAddress_ButStaffCan));
        await using var context = new TAGDBContext(options);

        Assert.Equal(400, Status(await CreateController(context, OwnerId).PutVenue(PublicVenue, Update(PublicVenue, PrivateContactAddress))));
        Assert.Equal(400, Status(await CreateController(context, OwnerId).PutVenue(PublicVenue, Update(PublicVenue, DraftVenueAddress))));
        Assert.Equal(204, Status(await CreateController(context, OwnerId).PutVenue(PublicVenue, Update(PublicVenue, PublicVenueAddress))));
        Assert.Equal(204, Status(await CreateController(context, StaffId).PutVenue(PublicVenue, Update(PublicVenue, FreeAddress))));
    }

    [Fact]
    public async Task AnOwner_CannotPointAVenueAtAnotherWebsiteOrPhone_ButCanKeepOrClearThem()
    {
        var options = await SeedAsync(nameof(AnOwner_CannotPointAVenueAtAnotherWebsiteOrPhone_ButCanKeepOrClearThem));
        await using var context = new TAGDBContext(options);
        var controller = CreateController(context, OwnerId);

        var otherLink = Update(PublicVenue, PublicVenueAddress);
        otherLink.ExternalLinkID = 9;
        var otherPhone = Update(PublicVenue, PublicVenueAddress);
        otherPhone.PhoneContactID = 9;

        Assert.Equal(400, Status(await controller.PutVenue(PublicVenue, otherLink)));
        Assert.Equal(400, Status(await controller.PutVenue(PublicVenue, otherPhone)));
        Assert.Equal(204, Status(await controller.PutVenue(PublicVenue, Update(PublicVenue, PublicVenueAddress))));
    }

    [Fact]
    public async Task ANewVenue_NeedsAFreshAddress_AndNoWebsiteOrPhoneRows()
    {
        var options = await SeedAsync(nameof(ANewVenue_NeedsAFreshAddress_AndNoWebsiteOrPhoneRows));
        await using var context = new TAGDBContext(options);
        var controller = CreateController(context, StrangerId);
        VenueCreateRequest Create(int addressId) => new() { Name = "Brand New Hall", AddressID = addressId };

        Assert.Equal(400, Status((await controller.PostVenue(Create(PublicVenueAddress))).Result));
        Assert.Equal(400, Status((await controller.PostVenue(Create(PrivateContactAddress))).Result));
        var withLink = Create(FreeAddress);
        withLink.ExternalLinkID = 5;
        Assert.Equal(400, Status((await controller.PostVenue(withLink)).Result));
        Assert.Equal(201, Status((await controller.PostVenue(Create(FreeAddress))).Result));
    }

    // ---- events of a venue that is not public

    [Fact]
    public async Task TheEventsOfAVenueThatIsNotPublic_AreNotPublic()
    {
        var options = await SeedAsync(nameof(TheEventsOfAVenueThatIsNotPublic_AreNotPublic));
        await using (var seed = new TAGDBContext(options))
        {
            var start = DateTime.UtcNow.AddDays(5);
            Event Make(int id, int venueId) => new()
            {
                EventID = id,
                Title = $"Event {id}",
                Description = "d",
                PointOfContact = "p",
                Path = $"event-{id}",
                ArtistID = null,
                VenueID = venueId,
                StartTime = start,
                EndTime = start.AddHours(2),
                Doors = start,
                StatusID = EventStatus.Published,
            };
            seed.Events.AddRange(Make(1, PublicVenue), Make(2, DraftVenue), Make(3, BlockedVenue));
            await seed.SaveChangesAsync();
        }

        await using var context = new TAGDBContext(options);
        var anonymous = SignedIn(new EventController(context), null);

        var listed = Assert.IsType<OkObjectResult>((await anonymous.GetEvents()).Result);
        Assert.Equal(new object[] { "Event 1" }, ((IEnumerable<object>)listed.Value!).Select(item => Prop(item, "Title")).ToArray());
        Assert.IsType<OkObjectResult>((await anonymous.GetEvent(1)).Result);
        Assert.IsType<NotFoundResult>((await anonymous.GetEvent(2)).Result);
        Assert.IsType<NotFoundResult>((await anonymous.GetEvent(3)).Result);
        Assert.IsType<OkObjectResult>((await anonymous.GetEventByPath("event-1")).Result);
        Assert.IsType<NotFoundResult>((await anonymous.GetEventByPath("event-2")).Result);
        Assert.IsType<NotFoundResult>((await anonymous.GetEventByPath("event-3")).Result);

        // The venue's owner can still open their own event while the venue is a draft.
        Assert.IsType<OkObjectResult>((await SignedIn(new EventController(context), DraftOwnerId).GetEvent(2)).Result);
    }
}
