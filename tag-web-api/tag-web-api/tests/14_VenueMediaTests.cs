using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TAGWEBAPI.Controllers;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;
using Xunit;
using static tagApiUnitTests.VenueTestSupport;

namespace tagApiUnitTests;

// Story #333: a venue's active profile and cover pictures, and who may change venue media. Database names start with
// "venuemedia-" because they are shared across test classes.
public class VenueMediaTests
{
    private const int OwnerId = 10;
    private const int OtherUserId = 20;
    private const int StaffId = 99;
    private const string Host = "https://store.example/tagpictures/";

    private static string Url(int venueId, string kind, string file = "photo.jpg") => $"{Host}platformpics/venue/{venueId}/{kind}/{file}";

    private static async Task<DbContextOptions<TAGDBContext>> SeedAsync(string name, int? ownerUserId = OwnerId)
    {
        var options = CreateOptions($"venuemedia-{name}");
        await VenueTestSupport.SeedAsync(options, new Venue { VenueID = 1, Name = "Test Hall", AddressID = AddressId }, ownerUserId, StaffId);

        await using var context = new TAGDBContext(options);
        context.Pictures.AddRange(
            new Picture { PictureID = 11, URL = Url(1, "profile"), NormalizedURL = Url(1, "profile").ToLowerInvariant() },
            new Picture { PictureID = 12, URL = Url(1, "cover"), NormalizedURL = Url(1, "cover").ToLowerInvariant() },
            new Picture { PictureID = 13, URL = Url(1, "profile", "second.png"), NormalizedURL = Url(1, "profile", "second.png").ToLowerInvariant() },
            new Picture { PictureID = 21, URL = Url(2, "profile"), NormalizedURL = Url(2, "profile").ToLowerInvariant() },
            new Picture { PictureID = 22, URL = Url(1, "profile", "2026-01-01T00-00-00-000Z/old.jpg"), NormalizedURL = Url(1, "profile", "2026-01-01T00-00-00-000Z/old.jpg").ToLowerInvariant() },
            new Picture { PictureID = 23, URL = "https://elsewhere.example/other/photo.jpg", NormalizedURL = "https://elsewhere.example/other/photo.jpg" });
        await context.SaveChangesAsync();
        return options;
    }

    private static void AssertStatus(object? result, int status)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(status, objectResult.StatusCode);
    }

    private static async Task<(int? Profile, int? Cover)> ReadActiveAsync(DbContextOptions<TAGDBContext> options)
    {
        await using var verify = new TAGDBContext(options);
        var venue = await verify.Set<Venue>().SingleAsync();
        return (venue.ProfilePicID, venue.CoverPicID);
    }

    // ---- PUT /api/venue/{id}/pictures/{kind}

    [Fact]
    public async Task SetActive_ByOwner_SavesTheProfileAndTheCoverSeparately()
    {
        var options = await SeedAsync(nameof(SetActive_ByOwner_SavesTheProfileAndTheCoverSeparately));
        await using var context = new TAGDBContext(options);
        var controller = CreateController(context, OwnerId);

        var profile = Assert.IsType<OkObjectResult>(await controller.PutActivePicture(1, "profile", new VenueActivePictureRequest { PictureID = 11 }));
        var cover = Assert.IsType<OkObjectResult>(await controller.PutActivePicture(1, "cover", new VenueActivePictureRequest { PictureID = 12 }));

        Assert.Equal(11, Prop(profile.Value, "profilePicID"));
        Assert.Equal((11, 12), (Prop(cover.Value, "profilePicID"), Prop(cover.Value, "coverPicID")));
        Assert.Equal((11, 12), await ReadActiveAsync(options));
    }

    [Fact]
    public async Task SetActive_TheKindIsNotCaseSensitive_AndANewPictureReplacesTheOld()
    {
        var options = await SeedAsync(nameof(SetActive_TheKindIsNotCaseSensitive_AndANewPictureReplacesTheOld));
        await using var context = new TAGDBContext(options);
        var controller = CreateController(context, OwnerId);

        Assert.IsType<OkObjectResult>(await controller.PutActivePicture(1, "Profile", new VenueActivePictureRequest { PictureID = 11 }));
        Assert.IsType<OkObjectResult>(await controller.PutActivePicture(1, "PROFILE", new VenueActivePictureRequest { PictureID = 13 }));

        Assert.Equal((13, null), await ReadActiveAsync(options));
    }

    [Fact]
    public async Task SetActive_WithANullPicture_ClearsIt()
    {
        var options = await SeedAsync(nameof(SetActive_WithANullPicture_ClearsIt));
        await using var context = new TAGDBContext(options);
        var controller = CreateController(context, OwnerId);
        await controller.PutActivePicture(1, "profile", new VenueActivePictureRequest { PictureID = 11 });

        Assert.IsType<OkObjectResult>(await controller.PutActivePicture(1, "profile", new VenueActivePictureRequest { PictureID = null }));

        Assert.Equal((null, null), await ReadActiveAsync(options));
    }

    [Fact]
    public async Task SetActive_ByStaff_CanChangeAnUnownedVenue()
    {
        var options = await SeedAsync(nameof(SetActive_ByStaff_CanChangeAnUnownedVenue), ownerUserId: null);
        await using var context = new TAGDBContext(options);

        Assert.IsType<OkObjectResult>(await CreateController(context, StaffId).PutActivePicture(1, "cover", new VenueActivePictureRequest { PictureID = 12 }));

        Assert.Equal((null, 12), await ReadActiveAsync(options));
    }

    [Fact]
    public async Task SetActive_ByNonOwner_Returns403AndChangesNothing()
    {
        var options = await SeedAsync(nameof(SetActive_ByNonOwner_Returns403AndChangesNothing));
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OtherUserId).PutActivePicture(1, "profile", new VenueActivePictureRequest { PictureID = 11 });

        AssertStatus(result, StatusCodes.Status403Forbidden);
        Assert.Equal((null, null), await ReadActiveAsync(options));
    }

    [Fact]
    public async Task SetActive_PermissionIsCheckedBeforeThePictureIsValidated()
    {
        var options = await SeedAsync(nameof(SetActive_PermissionIsCheckedBeforeThePictureIsValidated));
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OtherUserId).PutActivePicture(1, "profile", new VenueActivePictureRequest { PictureID = 999 });

        AssertStatus(result, StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task SetActive_Anonymous_Returns401_AndAMissingVenueReturns404()
    {
        var options = await SeedAsync(nameof(SetActive_Anonymous_Returns401_AndAMissingVenueReturns404));
        await using var context = new TAGDBContext(options);

        Assert.IsType<UnauthorizedResult>(await CreateController(context, userId: null).PutActivePicture(1, "profile", new VenueActivePictureRequest { PictureID = 11 }));
        Assert.IsType<NotFoundResult>(await CreateController(context, OwnerId).PutActivePicture(404, "profile", new VenueActivePictureRequest { PictureID = 11 }));
    }

    [Theory]
    [InlineData("logo")]
    [InlineData("gallery")]
    [InlineData("banner")]
    [InlineData("")]
    public async Task SetActive_OnlyKnowsProfileAndCover(string kind)
    {
        var options = await SeedAsync($"{nameof(SetActive_OnlyKnowsProfileAndCover)}-{kind}");
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OwnerId).PutActivePicture(1, kind, new VenueActivePictureRequest { PictureID = 11 });

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory]
    [InlineData(999, "profile")] // no such picture
    [InlineData(21, "profile")] // another venue's folder
    [InlineData(12, "profile")] // the cover folder, not the profile folder
    [InlineData(11, "cover")] // the profile folder, not the cover folder
    [InlineData(22, "profile")] // an archived older version
    [InlineData(23, "profile")] // a file somewhere else
    public async Task SetActive_RejectsAPictureThatIsNotInThisVenuesFolder(int pictureId, string kind)
    {
        var options = await SeedAsync($"{nameof(SetActive_RejectsAPictureThatIsNotInThisVenuesFolder)}-{pictureId}-{kind}");
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OwnerId).PutActivePicture(1, kind, new VenueActivePictureRequest { PictureID = pictureId });

        AssertStatus(result, StatusCodes.Status400BadRequest);
        Assert.Equal((null, null), await ReadActiveAsync(options));
    }

    [Fact]
    public async Task SetActive_RequiresABody()
    {
        var options = await SeedAsync(nameof(SetActive_RequiresABody));
        await using var context = new TAGDBContext(options);

        AssertStatus(await CreateController(context, OwnerId).PutActivePicture(1, "profile", null!), StatusCodes.Status400BadRequest);
    }

    // ---- the folder rule

    [Theory]
    [InlineData("https://store.example/tagpictures/platformpics/venue/1/profile/a.jpg", 1, "profile", true)]
    [InlineData("https://store.example/tagpictures/platformpics/Venue/1/Profile/a.jpg", 1, "profile", true)]
    [InlineData("https://store.example/tagpictures/platformpics/venue/1/profile/a%20b.jpg?x=1", 1, "profile", true)]
    [InlineData("https://store.example/tagpictures/platformpics/venue/12/profile/a.jpg", 1, "profile", false)]
    [InlineData("https://store.example/tagpictures/platformpics/venue/1/profile/a.jpg", 12, "profile", false)]
    [InlineData("https://store.example/tagpictures/platformpics/venue/1/profile/", 1, "profile", false)]
    [InlineData("https://store.example/tagpictures/platformpics/venue/1/profile/2026/a.jpg", 1, "profile", false)]
    [InlineData("https://store.example/tagpictures/platformpics/artist/1/profile/a.jpg", 1, "profile", false)]
    [InlineData("not a url", 1, "profile", false)]
    [InlineData("", 1, "profile", false)]
    public void IsPictureInVenueFolder_OnlyAcceptsAFileDirectlyInTheVenuesFolder(string url, int venueId, string kind, bool expected)
    {
        var picture = new Picture { URL = url, NormalizedURL = url };

        Assert.Equal(expected, VenueController.IsPictureInVenueFolder(picture, venueId, kind));
    }

    // ---- the venue as the pages load it

    [Fact]
    public async Task GetVenueById_ReturnsTheActivePictures()
    {
        var options = await SeedAsync(nameof(GetVenueById_ReturnsTheActivePictures));
        await using (var context = new TAGDBContext(options))
        {
            var controller = CreateController(context, OwnerId);
            await controller.PutActivePicture(1, "profile", new VenueActivePictureRequest { PictureID = 11 });
            await controller.PutActivePicture(1, "cover", new VenueActivePictureRequest { PictureID = 12 });
        }

        await using var read = new TAGDBContext(options);
        var result = await CreateController(read, OwnerId).GetVenueById(1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(11, Prop(ok.Value, "ProfilePicID"));
        Assert.Equal(12, Prop(ok.Value, "CoverPicID"));
        Assert.Equal(Url(1, "profile"), (Prop(ok.Value, "ProfilePic") as Picture)?.URL);
        Assert.Equal(Url(1, "cover"), (Prop(ok.Value, "CoverPic") as Picture)?.URL);
    }

    [Fact]
    public async Task GetVenueById_WithNoActivePictures_ReturnsNulls()
    {
        var options = await SeedAsync(nameof(GetVenueById_WithNoActivePictures_ReturnsNulls));
        await using var context = new TAGDBContext(options);

        var ok = Assert.IsType<OkObjectResult>((await CreateController(context, OwnerId).GetVenueById(1)).Result);

        Assert.Null(Prop(ok.Value, "ProfilePicID"));
        Assert.Null(Prop(ok.Value, "CoverPic"));
    }

    // ---- the logo

    private static LogoController CreateLogoController(TAGDBContext context, int userId)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Media:MaxLogoBytes"] = "5242880" }).Build();
        return new LogoController(context, config)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test")),
                },
            },
        };
    }

    [Fact]
    public async Task AssignLogo_ByAVenueOwner_Succeeds()
    {
        var options = await SeedAsync(nameof(AssignLogo_ByAVenueOwner_Succeeds));
        await using var context = new TAGDBContext(options);
        context.Pictures.Add(new Picture { PictureID = 31, URL = Url(1, "logo", "logo.png"), NormalizedURL = Url(1, "logo", "logo.png") });
        await context.SaveChangesAsync();

        var result = await CreateLogoController(context, OwnerId).AssignLogo("venue", 1, new LogoController.LogoAssociationRequest { PictureID = 31, ContentType = "image/png" });

        Assert.IsType<OkObjectResult>(result.Result);
        await using var verify = new TAGDBContext(options);
        Assert.Equal(31, (await verify.Set<Venue>().SingleAsync()).LogoPicID);
    }

    [Fact]
    public async Task AssignLogo_ByAStranger_IsForbidden_AndByStaffIsAllowed()
    {
        var options = await SeedAsync(nameof(AssignLogo_ByAStranger_IsForbidden_AndByStaffIsAllowed));
        await using var context = new TAGDBContext(options);
        context.Pictures.Add(new Picture { PictureID = 31, URL = Url(1, "logo", "logo.png"), NormalizedURL = Url(1, "logo", "logo.png") });
        await context.SaveChangesAsync();
        var request = new LogoController.LogoAssociationRequest { PictureID = 31, ContentType = "image/png" };

        Assert.IsType<ForbidResult>(await CreateLogoController(context, OtherUserId).AssignLogo("venue", 1, request) is { Result: var stranger } ? stranger : null);
        await using (var verify = new TAGDBContext(options))
        {
            Assert.Null((await verify.Set<Venue>().SingleAsync()).LogoPicID);
        }

        Assert.IsType<OkObjectResult>((await CreateLogoController(context, StaffId).AssignLogo("venue", 1, request)).Result);
    }
}
