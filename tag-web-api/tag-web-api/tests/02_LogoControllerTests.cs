using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TAGWEBAPI.Controllers;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;
using Xunit;

namespace tagApiUnitTests;

public class LogoControllerTests
{
    private static TAGDBContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<TAGDBContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new TAGDBContext(options);
    }

    private static LogoController CreateController(TAGDBContext context)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Media:MaxLogoBytes"] = "5242880",
            })
            .Build();

        return new LogoController(context, config);
    }

    [Fact]
    public async Task AssignLogo_ReplacesActiveLogo_AndPreservesHistory()
    {
        await using var context = CreateContext(nameof(AssignLogo_ReplacesActiveLogo_AndPreservesHistory));

        context.Users.Add(new User
        {
            UserID = 7,
            EmailOne = "admin@example.com",
            Username = "admin",
            IsPublished = true,
            Moderator = true,
        });

        var original = new Picture { PictureID = 101, URL = "https://cdn.example.com/artist-logo-old.png", NormalizedURL = "https://cdn.example.com/artist-logo-old.png" };
        var replacement = new Picture { PictureID = 102, URL = "https://cdn.example.com/artist-logo-new.png", NormalizedURL = "https://cdn.example.com/artist-logo-new.png" };
        context.Pictures.AddRange(original, replacement);
        context.Artists.Add(new Artist
        {
            ArtistID = 42,
            Title = "Logo Test Artist",
            Path = "logo-test-artist",
            Applied = DateTime.UtcNow,
            Since = DateTime.UtcNow,
            LogoPicID = 101,
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "7") }, "TestAuth"))
            }
        };
        var result = await controller.AssignLogo("artist", 42, new LogoController.LogoAssociationRequest { PictureID = 102, ContentType = "image/png" });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = ok.Value;
        Assert.NotNull(body);

        var artist = await context.Artists.AsNoTracking().FirstAsync(a => a.ArtistID == 42);
        Assert.Equal(102, artist.LogoPicID);

        var history = await context.LogoHistory.AsNoTracking().OrderByDescending(h => h.CreatedUtc).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Contains(history, h => h.PictureID == 101 && h.IsArchived && !h.IsActive);
        Assert.Contains(history, h => h.PictureID == 102 && h.IsActive && !h.IsArchived);
    }

    [Fact]
    public async Task RestoreLogo_RestoresPreviousVersionAsActive()
    {
        await using var context = CreateContext(nameof(RestoreLogo_RestoresPreviousVersionAsActive));

        context.Users.Add(new User
        {
            UserID = 8,
            EmailOne = "restore-admin@example.com",
            Username = "restore-admin",
            IsPublished = true,
            Moderator = true,
        });

        var oldPicture = new Picture { PictureID = 201, URL = "https://cdn.example.com/old-logo.png", NormalizedURL = "https://cdn.example.com/old-logo.png" };
        var newPicture = new Picture { PictureID = 202, URL = "https://cdn.example.com/new-logo.png", NormalizedURL = "https://cdn.example.com/new-logo.png" };
        context.Pictures.AddRange(oldPicture, newPicture);
        context.Artists.Add(new Artist
        {
            ArtistID = 55,
            Title = "Restore Artist",
            Path = "restore-artist",
            Applied = DateTime.UtcNow,
            Since = DateTime.UtcNow,
            LogoPicID = 202,
        });

        context.LogoHistory.AddRange(
            new LogoHistory { EntityType = "artist", EntityID = 55, PictureID = 201, IsActive = false, IsArchived = true, CreatedUtc = DateTime.UtcNow.AddMinutes(-5) },
            new LogoHistory { EntityType = "artist", EntityID = 55, PictureID = 202, IsActive = true, CreatedUtc = DateTime.UtcNow.AddMinutes(-2) });
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "8") }, "TestAuth"))
            }
        };
        var restoreResult = await controller.RestoreLogo("artist", 55, 1);

        var ok = Assert.IsType<OkObjectResult>(restoreResult.Result);
        Assert.NotNull(ok.Value);

        var artist = await context.Artists.AsNoTracking().FirstAsync(a => a.ArtistID == 55);
        Assert.Equal(201, artist.LogoPicID);

        var restored = await context.LogoHistory.AsNoTracking().SingleAsync(h => h.LogoHistoryID == 1 && h.EntityType == "artist" && h.EntityID == 55);
        Assert.True(restored.IsActive);
    }

    [Fact]
    public async Task AssignLogo_RejectsUnauthorizedUser()
    {
        await using var context = CreateContext(nameof(AssignLogo_RejectsUnauthorizedUser));

        var picture = new Picture { PictureID = 301, URL = "https://cdn.example.com/unauthorized.png", NormalizedURL = "https://cdn.example.com/unauthorized.png" };
        context.Pictures.Add(picture);
        context.Users.Add(new User
        {
            UserID = 1,
            EmailOne = "user@example.com",
            Username = "member",
            IsPublished = true,
            Moderator = false,
        });
        context.Artists.Add(new Artist
        {
            ArtistID = 66,
            Title = "Locked Artist",
            Path = "locked-artist",
            Applied = DateTime.UtcNow,
            Since = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "1") }, "TestAuth"))
            }
        };

        var result = await controller.AssignLogo("artist", 66, new LogoController.LogoAssociationRequest { PictureID = 301, ContentType = "image/png" });

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetArtistProfile_IncludesLogoPic()
    {
        await using var context = CreateContext(nameof(GetArtistProfile_IncludesLogoPic));

        var logo = new Picture { PictureID = 401, URL = "https://cdn.example.com/logo.png", NormalizedURL = "https://cdn.example.com/logo.png" };
        context.Pictures.Add(logo);
        context.Artists.Add(new Artist
        {
            ArtistID = 77,
            Title = "Artist With Logo",
            Path = "artist-with-logo",
            Applied = DateTime.UtcNow,
            Since = DateTime.UtcNow,
            IsPublished = true,
            IsModerationBlocked = false,
            LogoPicID = 401,
        });
        await context.SaveChangesAsync();

        var controller = new ArtistController(context, new Microsoft.Extensions.Logging.Abstractions.NullLogger<ArtistController>());
        var result = await controller.Get("artist-with-logo");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = ok.Value;
        Assert.NotNull(payload);

        var logoPicValue = payload.GetType().GetProperty("logoPic")?.GetValue(payload);
        Assert.NotNull(logoPicValue);
        var url = logoPicValue!.GetType().GetProperty("URL")?.GetValue(logoPicValue)?.ToString();
        Assert.Equal("https://cdn.example.com/logo.png", url);
    }
}
