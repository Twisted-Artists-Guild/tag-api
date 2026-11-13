using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TAGWEBAPI.Controllers;
using TAGWEBAPI.Data;
using TAGWEBAPI.Hubs;
using TAGWEBAPI.Models;
using Xunit;
using static tagApiUnitTests.VenueTestSupport;

namespace tagApiUnitTests;

// Story #339: venues work with the rest of the platform (anonymous reads, feed links, comments as a venue, moderation).
public class VenuePlatformIntegrationTests
{
    private const int OwnerId = 10;
    private const int OtherUserId = 11;

    // Venue 1 is public and owned by OwnerId; 2 is a draft; 3 is blocked.
    private static async Task<DbContextOptions<TAGDBContext>> SeedAsync(string databaseName)
    {
        var options = CreateOptions(databaseName);
        await VenueTestSupport.SeedAsync(
            options,
            new Venue { VenueID = 1, Name = "Terminal West", AddressID = AddressId, IsPublished = true, Description = "<p>Big room</p>", Description_Plaintext = "Big room" },
            OwnerId);

        await using var seed = new TAGDBContext(options);
        seed.Set<Venue>().Add(new Venue { VenueID = 2, Name = "Draft Hall", AddressID = AddressId, IsPublished = false });
        seed.Set<Venue>().Add(new Venue { VenueID = 3, Name = "Blocked Hall", AddressID = AddressId, IsPublished = true, IsModerationBlocked = true });
        seed.Set<Linker_UserToVenue>().Add(new Linker_UserToVenue { UserID = OwnerId, VenueID = 2, Role = Linker_UserToVenue.OwnerRole });
        seed.Set<Linker_UserToVenue>().Add(new Linker_UserToVenue { UserID = OwnerId, VenueID = 3, Role = Linker_UserToVenue.OwnerRole });
        seed.Users.Add(new User { UserID = OwnerId, EmailOne = "owner@example.com", Username = "owner" });
        seed.Users.Add(new User { UserID = OtherUserId, EmailOne = "other@example.com", Username = "other" });
        seed.Set<NextAuthUser>().Add(new NextAuthUser { Id = OwnerId, Name = "Owner Person", Image = "owner.jpg" });
        seed.Set<NextAuthUser>().Add(new NextAuthUser { Id = OtherUserId, Name = "Other Person" });
        await seed.SaveChangesAsync();
        return options;
    }

    private static ControllerContext AsUser(int? userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(userId.HasValue
                ? new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) }, "test")
                : new ClaimsIdentity()),
        },
    };

    private static IEnumerable<object> Items(ActionResult<IEnumerable<Venue>> result) =>
        ((System.Collections.IEnumerable)Assert.IsType<OkObjectResult>(result.Result).Value!).Cast<object>();

    // ---- anonymous venue reads

    [Fact]
    public async Task Anonymous_ListShowsOnlyPublishedUnblockedVenues()
    {
        var options = await SeedAsync(nameof(Anonymous_ListShowsOnlyPublishedUnblockedVenues));
        await using var context = new TAGDBContext(options);

        var venues = Items(await CreateController(context, null).GetVenues()).ToList();

        var only = Assert.Single(venues);
        Assert.Equal(1, Prop(only, "VenueID"));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(99, false)]
    public async Task Anonymous_GetById_OnlyReturnsPublicVenues(int id, bool visible)
    {
        var options = await SeedAsync($"{nameof(Anonymous_GetById_OnlyReturnsPublicVenues)}-{id}");
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, null).GetVenue(id);

        if (visible) { Assert.IsType<OkObjectResult>(result.Result); } else { Assert.IsType<NotFoundResult>(result.Result); }
    }

    [Theory]
    [InlineData("terminal-west", true)]
    [InlineData("draft-hall", false)]
    [InlineData("blocked-hall", false)]
    [InlineData("no-such-venue", false)]
    public async Task Anonymous_GetBySlug_OnlyReturnsPublicVenues(string slug, bool visible)
    {
        var options = await SeedAsync($"{nameof(Anonymous_GetBySlug_OnlyReturnsPublicVenues)}-{slug}");
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, null).GetVenueBySlug(slug);

        if (visible) { Assert.IsType<OkObjectResult>(result.Result); } else { Assert.IsType<NotFoundResult>(result.Result); }
    }

    [Fact]
    public async Task Owner_StillSeesTheirDraftBySlug()
    {
        var options = await SeedAsync(nameof(Owner_StillSeesTheirDraftBySlug));
        await using var context = new TAGDBContext(options);

        var result = await CreateController(context, OwnerId).GetVenueBySlug("draft-hall");

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public void OnlyThePublicReadEndpointsAreAnonymous_WritesStayProtected()
    {
        Assert.NotNull(typeof(VenueController).GetCustomAttribute<AuthorizeAttribute>());

        var methods = typeof(VenueController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var anonymous = methods.Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() != null).Select(m => m.Name).OrderBy(n => n).ToList();

        Assert.Equal(new[] { nameof(VenueController.GetVenue), nameof(VenueController.GetVenueBySlug), nameof(VenueController.GetVenues) }.OrderBy(n => n), anonymous);
        Assert.All(
            methods.Where(m => m.GetCustomAttributes().Any(a => a is HttpPostAttribute or HttpPutAttribute or HttpPatchAttribute or HttpDeleteAttribute)),
            m => Assert.Null(m.GetCustomAttribute<AllowAnonymousAttribute>()));
    }

    // ---- feed links and shared previews

    private static async Task<(FeedController Controller, TAGDBContext Context)> CreateFeedAsync(string databaseName)
    {
        var options = await SeedAsync(databaseName);
        var context = new TAGDBContext(options);
        return (new FeedController(context), context);
    }

    private static FeedPostSummaryDTO Created(ActionResult<FeedPostSummaryDTO> result) =>
        Assert.IsType<FeedPostSummaryDTO>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);

    [Fact]
    public async Task Feed_SharingAPublicVenue_LinksToTheVenuePage()
    {
        var (feed, context) = await CreateFeedAsync(nameof(Feed_SharingAPublicVenue_LinksToTheVenuePage));
        await using var _ = context;

        var post = Created(await feed.Create(new CreateFeedPostDTO { Body = "Go here", PostType = "ShareVenue", SharedEntityType = "Venue", SharedEntityID = 1 }, OwnerId));

        Assert.Equal("/venue/terminal-west", post.SharedURL);
        Assert.Equal("/venue/terminal-west", VenueTestSupport.Prop(post.SharedEntityPreview, "path"));
        Assert.Equal("Terminal West", VenueTestSupport.Prop(post.SharedEntityPreview, "title"));
        Assert.Equal("Big room", VenueTestSupport.Prop(post.SharedEntityPreview, "byline"));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(99)]
    public async Task Feed_SharingANonPublicVenue_HasNoLinkAndNoPreview(int venueId)
    {
        var (feed, context) = await CreateFeedAsync($"{nameof(Feed_SharingANonPublicVenue_HasNoLinkAndNoPreview)}-{venueId}");
        await using var _ = context;

        var post = Created(await feed.Create(new CreateFeedPostDTO { PostType = "ShareVenue", SharedEntityType = "Venue", SharedEntityID = venueId }, OwnerId));

        Assert.Null(post.SharedURL);
        Assert.Null(post.SharedEntityPreview);
    }

    [Fact]
    public async Task Feed_OldPostsWithTheBrokenVenueAddress_AreFixedWhenRead()
    {
        var options = await SeedAsync(nameof(Feed_OldPostsWithTheBrokenVenueAddress_AreFixedWhenRead));
        await using (var seed = new TAGDBContext(options))
        {
            seed.Set<FeedPost>().Add(new FeedPost { FeedPostID = 50, AuthorUserID = OwnerId, PostType = "ShareVenue", SharedEntityType = "Venue", SharedEntityID = 1, SharedURL = "/venues/1" });
            await seed.SaveChangesAsync();
        }

        await using var context = new TAGDBContext(options);
        var result = await new FeedController(context).GetById(50);

        Assert.Equal("/venue/terminal-west", result.Value!.SharedURL);
    }

    [Fact]
    public async Task Feed_PostedAsAPublicVenue_LinksTheAuthorToTheVenuePage()
    {
        var options = await SeedAsync(nameof(Feed_PostedAsAPublicVenue_LinksTheAuthorToTheVenuePage));
        await using (var seed = new TAGDBContext(options))
        {
            seed.Set<FeedPost>().AddRange(
                new FeedPost { FeedPostID = 60, AuthorUserID = OwnerId, AuthorEntityType = "Venue", AuthorEntityID = 1 },
                new FeedPost { FeedPostID = 61, AuthorUserID = OwnerId, AuthorEntityType = "Venue", AuthorEntityID = 2 });
            await seed.SaveChangesAsync();
        }

        await using var context = new TAGDBContext(options);
        var feed = new FeedController(context);

        var publicPost = (await feed.GetById(60)).Value!;
        Assert.Equal("Terminal West", publicPost.AuthorEntityName);
        Assert.Equal("/venue/terminal-west", publicPost.AuthorEntityPath);

        var draftPost = (await feed.GetById(61)).Value!;
        Assert.Null(draftPost.AuthorEntityName);
        Assert.Null(draftPost.AuthorEntityPath);
    }

    // ---- comments posted as a venue

    private static CommentsController CreateComments(TAGDBContext context, int userId) =>
        new(context, NullLogger<CommentsController>.Instance, new Mock<IHubContext<MessagingHub>>().Object)
        {
            ControllerContext = AsUser(userId),
        };

    private static CreateCommentRequest AsVenue(int? venueId) => new()
    {
        TargetType = CommentTargetType.FeedPost,
        TargetId = 1,
        Content = "Doors at 7",
        AuthorContextId = $"venue-{venueId}",
        AuthorEntityType = "venue",
        AuthorEntityId = venueId,
    };

    [Fact]
    public async Task Comment_AsAVenueTheUserOwns_ShowsTheVenueNameAndLogo()
    {
        var options = await SeedAsync(nameof(Comment_AsAVenueTheUserOwns_ShowsTheVenueNameAndLogo));
        await using (var seed = new TAGDBContext(options))
        {
            seed.Pictures.Add(new Picture { PictureID = 70, URL = "https://cdn.example.com/venue-1.jpg" });
            (await seed.Set<Venue>().FindAsync(1))!.LogoPicID = 70;
            await seed.SaveChangesAsync();
        }

        await using var context = new TAGDBContext(options);
        var controller = CreateComments(context, OwnerId);

        var created = await controller.CreateComment(AsVenue(1));

        var dto = Assert.IsType<CommentDto>(Assert.IsType<CreatedAtActionResult>(created.Result).Value);
        Assert.Equal("venue", dto.AuthorEntityType);
        Assert.Equal(1, dto.AuthorEntityId);
        Assert.Equal("Terminal West", dto.AuthorDisplayName);
        Assert.Equal("https://cdn.example.com/venue-1.jpg", dto.AuthorImage);
        Assert.Equal(OwnerId, dto.UserId);
    }

    [Fact]
    public async Task Comment_AsAVenueTheUserDoesNotOwn_IsForbidden()
    {
        var options = await SeedAsync(nameof(Comment_AsAVenueTheUserDoesNotOwn_IsForbidden));
        await using var context = new TAGDBContext(options);

        var result = await CreateComments(context, OtherUserId).CreateComment(AsVenue(1));

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Empty(context.Comments);
    }

    [Fact]
    public async Task Comment_AsAVenueWithoutAnId_IsBadRequest()
    {
        var options = await SeedAsync(nameof(Comment_AsAVenueWithoutAnId_IsBadRequest));
        await using var context = new TAGDBContext(options);

        var result = await CreateComments(context, OwnerId).CreateComment(AsVenue(null));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Comment_AsAVenueThatIsNotPublic_IsBadRequest(int venueId)
    {
        var options = await SeedAsync($"{nameof(Comment_AsAVenueThatIsNotPublic_IsBadRequest)}-{venueId}");
        await using var context = new TAGDBContext(options);

        var result = await CreateComments(context, OwnerId).CreateComment(AsVenue(venueId));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(context.Comments);
    }

    [Fact]
    public async Task Comment_FromAVenueThatLaterBecameNonPublic_FallsBackToTheUser()
    {
        var options = await SeedAsync(nameof(Comment_FromAVenueThatLaterBecameNonPublic_FallsBackToTheUser));
        await using (var seed = new TAGDBContext(options))
        {
            seed.Comments.Add(new Comment { Id = 1, TargetType = CommentTargetType.FeedPost, TargetId = 1, UserId = OwnerId, Content = "old", AuthorEntityType = "venue", AuthorEntityId = 3, CreatedAt = DateTime.UtcNow });
            await seed.SaveChangesAsync();
        }

        await using var context = new TAGDBContext(options);
        var result = await CreateComments(context, OwnerId).GetComments(CommentTargetType.FeedPost, 1);

        var response = Assert.IsType<CommentsResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var dto = Assert.Single(response.Comments);
        Assert.Equal("user", dto.AuthorEntityType);
        Assert.Equal("Owner Person", dto.AuthorDisplayName);
    }

    // ---- moderation

    [Fact]
    public async Task Moderation_BlockingAReportedVenue_HidesItFromThePublic()
    {
        var options = await SeedAsync(nameof(Moderation_BlockingAReportedVenue_HidesItFromThePublic));
        await using (var seed = new TAGDBContext(options))
        {
            seed.ContentReports.Add(new ContentReport { ContentReportID = 1, ReporterUserID = OtherUserId, TargetType = "Venue", TargetID = 1, Description = "spam" });
            await seed.SaveChangesAsync();
        }

        await using var context = new TAGDBContext(options);
        await new ContentReportController(context).CreateAction(new CreateModerationActionDTO { ContentReportID = 1, ActionType = "Block" }, 1);

        await using var check = new TAGDBContext(options);
        Assert.True((await check.Set<Venue>().FindAsync(1))!.IsModerationBlocked);
        Assert.Empty(Items(await CreateController(check, null).GetVenues()));
    }
}
