using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Controllers;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;
using Xunit;
using static tagApiUnitTests.VenueTestSupport;

namespace tagApiUnitTests;

// Story #324: a venue can exist with only a name and an address, and carries a description
// and an optional, validated event-source URL.
public class VenueControllerTests
{
    [Fact]
    public async Task PostVenue_WithOnlyNameAndAddress_CreatesVenueWithoutContacts()
    {
        var options = CreateOptions(nameof(PostVenue_WithOnlyNameAndAddress_CreatesVenueWithoutContacts));
        await SeedAsync(options);
        await using var context = new TAGDBContext(options);
        var controller = CreateController(context);

        var result = await controller.PostVenue(new VenueCreateRequest { Name = "Unowned Hall", AddressID = AddressId });

        Assert.IsType<CreatedAtActionResult>(result.Result);
        var saved = await context.Set<Venue>().SingleAsync();
        Assert.Null(saved.ExternalLinkID);
        Assert.Null(saved.PhoneContactID);

        var read = Assert.IsType<OkObjectResult>((await controller.GetVenueById(saved.VenueID)).Result);
        Assert.Equal("Unowned Hall", Prop(read.Value, "Name"));
        Assert.Null(Prop(read.Value, "ExternalLinkID"));
        Assert.Null(Prop(read.Value, "PhoneContactID"));
        Assert.Null(Prop(read.Value, "EventSourceUrl"));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/events")]
    [InlineData("/relative/events")]
    [InlineData("//example.com/events")]
    [InlineData("not a url")]
    public async Task PostVenue_NonHttpEventSourceUrl_ReturnsBadRequest(string url)
    {
        var options = CreateOptions($"{nameof(PostVenue_NonHttpEventSourceUrl_ReturnsBadRequest)}-{url.Length}");
        await SeedAsync(options);
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context).PostVenue(new VenueCreateRequest { Name = "Bad Url Hall", AddressID = AddressId, EventSourceUrl = url });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(context.Set<Venue>());
    }

    [Fact]
    public async Task PostVenue_EventSourceUrlTooLong_ReturnsBadRequest()
    {
        var options = CreateOptions(nameof(PostVenue_EventSourceUrlTooLong_ReturnsBadRequest));
        await SeedAsync(options);
        await using var context = new TAGDBContext(options);
        var url = "https://example.com/" + new string('a', Venue.EventSourceUrlMaxLength);

        var result = await CreateController(context).PostVenue(new VenueCreateRequest { Name = "Long Url Hall", AddressID = AddressId, EventSourceUrl = url });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(" https://example.com/events ", "https://example.com/events")]
    [InlineData("http://example.com/calendar", "http://example.com/calendar")]
    [InlineData("   ", null)]
    [InlineData("", null)]
    public async Task PostVenue_EventSourceUrl_IsTrimmedAndEmptyBecomesNull(string input, string? expected)
    {
        var options = CreateOptions($"{nameof(PostVenue_EventSourceUrl_IsTrimmedAndEmptyBecomesNull)}-{input.Length}");
        await SeedAsync(options);
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context).PostVenue(new VenueCreateRequest { Name = "Url Hall", AddressID = AddressId, EventSourceUrl = input });

        Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(expected, (await context.Set<Venue>().SingleAsync()).EventSourceUrl);
    }

    [Fact]
    public async Task PostVenue_DescriptionTooLong_ReturnsBadRequest()
    {
        var options = CreateOptions(nameof(PostVenue_DescriptionTooLong_ReturnsBadRequest));
        await SeedAsync(options);
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context).PostVenue(new VenueCreateRequest { Name = "Wordy Hall", AddressID = AddressId, Description = new string('a', 20001) });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetVenueById_ReturnsPlaintextAndRichtextDescription()
    {
        var options = CreateOptions(nameof(GetVenueById_ReturnsPlaintextAndRichtextDescription));
        await SeedAsync(options, new Venue { VenueID = 1, Name = "Hall", AddressID = AddressId, IsPublished = true, Description = "<p>Hello <b>world</b></p>", EventSourceUrl = "https://example.com/events" });
        await using var context = new TAGDBContext(options);

        var read = Assert.IsType<OkObjectResult>((await CreateController(context).GetVenueById(1)).Result);

        // Plaintext is derived from the rich text when the database trigger has not populated it.
        Assert.Equal("Hello world", Prop(read.Value, "Description"));
        Assert.Equal("<p>Hello <b>world</b></p>", Prop(read.Value, "DescriptionRichtext"));
        Assert.Equal("https://example.com/events", Prop(read.Value, "EventSourceUrl"));
    }

    [Fact]
    public async Task GetVenueById_PrefersStoredPlaintextDescription()
    {
        var options = CreateOptions(nameof(GetVenueById_PrefersStoredPlaintextDescription));
        await SeedAsync(options, new Venue { VenueID = 1, Name = "Hall", AddressID = AddressId, IsPublished = true, Description = "<p>Rich</p>", Description_Plaintext = "Stored plain" });
        await using var context = new TAGDBContext(options);

        var read = Assert.IsType<OkObjectResult>((await CreateController(context).GetVenueById(1)).Result);

        Assert.Equal("Stored plain", Prop(read.Value, "Description"));
    }

    [Fact]
    public async Task PutVenue_InvalidEventSourceUrl_ReturnsBadRequestAndKeepsStoredValue()
    {
        var options = CreateOptions(nameof(PutVenue_InvalidEventSourceUrl_ReturnsBadRequestAndKeepsStoredValue));
        await SeedAsync(options, new Venue { VenueID = 1, Name = "Hall", AddressID = AddressId, EventSourceUrl = "https://example.com/events" }, ownerUserId: 1);
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context).PutVenue(1, new VenueUpdateRequest { VenueID = 1, Name = "Hall", AddressID = AddressId, EventSourceUrl = "javascript:alert(1)" });

        Assert.IsType<BadRequestObjectResult>(result);
        await using var verify = new TAGDBContext(options);
        Assert.Equal("https://example.com/events", (await verify.Set<Venue>().SingleAsync()).EventSourceUrl);
    }

    [Fact]
    public async Task PutVenue_AllowsClearingOptionalContactsAndSavesNewFields()
    {
        var options = CreateOptions(nameof(PutVenue_AllowsClearingOptionalContactsAndSavesNewFields));
        await SeedAsync(options, new Venue { VenueID = 1, Name = "Hall", AddressID = AddressId, ExternalLinkID = 5, PhoneContactID = 5 }, ownerUserId: 1);
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context).PutVenue(1, new VenueUpdateRequest
        {
            VenueID = 1,
            Name = "Hall",
            AddressID = AddressId,
            ExternalLinkID = null,
            PhoneContactID = null,
            Description = "<p>A cozy room</p>",
            EventSourceUrl = "https://example.com/calendar",
        });

        Assert.IsType<NoContentResult>(result);
        await using var verify = new TAGDBContext(options);
        var saved = await verify.Set<Venue>().SingleAsync();
        Assert.Null(saved.ExternalLinkID);
        Assert.Null(saved.PhoneContactID);
        Assert.Equal("<p>A cozy room</p>", saved.Description);
        Assert.Equal("https://example.com/calendar", saved.EventSourceUrl);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(12345)]
    public async Task PutVenue_MissingOrUnknownAddressId_ReturnsBadRequestInsteadOfFailingInTheDatabase(int addressId)
    {
        var options = CreateOptions($"{nameof(PutVenue_MissingOrUnknownAddressId_ReturnsBadRequestInsteadOfFailingInTheDatabase)}-{addressId}");
        await SeedAsync(options, new Venue { VenueID = 1, Name = "Hall", AddressID = AddressId }, ownerUserId: 1);
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context).PutVenue(1, new VenueUpdateRequest { VenueID = 1, Name = "Renamed", AddressID = addressId });

        Assert.IsType<BadRequestObjectResult>(result);
        await using var verify = new TAGDBContext(options);
        var stored = await verify.Set<Venue>().SingleAsync();
        Assert.Equal("Hall", stored.Name);
        Assert.Equal(AddressId, stored.AddressID);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12345)]
    public async Task PostVenue_MissingOrUnknownAddressId_ReturnsBadRequest(int addressId)
    {
        var options = CreateOptions($"{nameof(PostVenue_MissingOrUnknownAddressId_ReturnsBadRequest)}-{addressId}");
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context).PostVenue(new VenueCreateRequest { Name = "No Address Hall", AddressID = addressId });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(context.Set<Venue>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PutVenue_BlankName_ReturnsBadRequest(string? name)
    {
        var options = CreateOptions($"{nameof(PutVenue_BlankName_ReturnsBadRequest)}-{name?.Length}");
        await SeedAsync(options, new Venue { VenueID = 1, Name = "Hall", AddressID = AddressId }, ownerUserId: 1);
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context).PutVenue(1, new VenueUpdateRequest { VenueID = 1, Name = name, AddressID = AddressId });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // The wizard saves with PUT /api/venue/{id} and sends only foreign key IDs, never nested objects.
    // With nullable reference types enabled, a non-nullable navigation property is implicitly required
    // by model validation and the request fails with 400 ("The Address field is required.").
    [Fact]
    public void Venue_NavigationProperties_AreNullableSoIdOnlyRequestsPassValidation()
    {
        var nullability = new System.Reflection.NullabilityInfoContext();
        foreach (var name in new[] { nameof(Venue.Address), nameof(Venue.ExternalLink), nameof(Venue.PhoneContact), nameof(Venue.LogoPic) })
        {
            var property = typeof(Venue).GetProperty(name)!;
            Assert.Equal(System.Reflection.NullabilityState.Nullable, nullability.Create(property).WriteState);
        }

        // The address is still mandatory through its foreign key.
        Assert.Equal(typeof(int), typeof(Venue).GetProperty(nameof(Venue.AddressID))!.PropertyType);
    }

    [Fact]
    public async Task ReserveVenueSlug_DoesNotCreatePlaceholderWebsiteOrPhone()
    {
        var options = CreateOptions(nameof(ReserveVenueSlug_DoesNotCreatePlaceholderWebsiteOrPhone));
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context).ReserveVenueSlug(new VenueSlugReservationRequest { Slug = "fresh-venue", Title = "Fresh Venue" });

        Assert.IsType<CreatedAtActionResult>(result.Result);
        var venue = await context.Set<Venue>().SingleAsync();
        Assert.Null(venue.ExternalLinkID);
        Assert.Null(venue.PhoneContactID);
        Assert.Empty(context.Set<ExternalLink>());
        Assert.Empty(context.Set<PhoneContact>());
    }
}
