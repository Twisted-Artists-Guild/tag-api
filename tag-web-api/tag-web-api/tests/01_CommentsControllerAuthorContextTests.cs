using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TAGWEBAPI.Controllers;
using TAGWEBAPI.Data;
using TAGWEBAPI.Hubs;
using TAGWEBAPI.Models;
using Xunit;

namespace tagApiUnitTests;

// Verifies GET /api/comments resolves the "selected profile context" author identity
// (artist profile vs. base user) and does so via batched queries rather than one lookup per comment.
public class CommentsControllerAuthorContextTests
{
    private static TAGDBContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<TAGDBContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new TAGDBContext(options);
    }

    private static CommentsController CreateController(TAGDBContext context)
    {
        var hubContext = new Mock<IHubContext<MessagingHub>>();
        return new CommentsController(context, NullLogger<CommentsController>.Instance, hubContext.Object);
    }

    [Fact]
    public async Task GetComments_ArtistContext_ReturnsArtistTitleAndImage()
    {
        await using var context = CreateContext(nameof(GetComments_ArtistContext_ReturnsArtistTitleAndImage));

        context.Set<NextAuthUser>().Add(new NextAuthUser { Id = 1, Name = "BaseUser", Image = "user.jpg" });
        context.Pictures.Add(new Picture { PictureID = 10, URL = "https://cdn.example.com/artist-8.jpg" });
        context.Artists.Add(new Artist { ArtistID = 8, Title = "ArtStudio8", Path = "art-studio-8", Applied = DateTime.UtcNow, Since = DateTime.UtcNow, ProfilePicID = 10 });
        context.Comments.Add(new Comment
        {
            Id = 1,
            TargetType = CommentTargetType.Artist,
            TargetId = 8,
            UserId = 1,
            Content = "Great work!",
            AuthorContextId = "artist-8",
            AuthorEntityType = "artist",
            AuthorEntityId = 8,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var result = await controller.GetComments(CommentTargetType.Artist, 8);

        var response = Assert.IsType<CommentsResponse>(Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result).Value);
        var dto = Assert.Single(response.Comments);

        Assert.Equal("artist", dto.AuthorEntityType);
        Assert.Equal(8, dto.AuthorEntityId);
        Assert.Equal("ArtStudio8", dto.AuthorDisplayName);
        Assert.Equal("https://cdn.example.com/artist-8.jpg", dto.AuthorImage);
    }

    [Fact]
    public async Task GetComments_ArtistContextWithoutProfilePicture_ReturnsNullImageNotUserImage()
    {
        await using var context = CreateContext(nameof(GetComments_ArtistContextWithoutProfilePicture_ReturnsNullImageNotUserImage));

        // The base user DOES have an image, but the selected artist identity has none assigned (ProfilePicID = null).
        context.Set<NextAuthUser>().Add(new NextAuthUser { Id = 1, Name = "BaseUser", Image = "user.jpg" });
        context.Artists.Add(new Artist { ArtistID = 9, Title = "manishb", Path = "manishb", Applied = DateTime.UtcNow, Since = DateTime.UtcNow, ProfilePicID = null });
        context.Comments.Add(new Comment
        {
            Id = 2,
            TargetType = CommentTargetType.Artist,
            TargetId = 9,
            UserId = 1,
            Content = "No profile pic yet",
            AuthorContextId = "artist-9",
            AuthorEntityType = "artist",
            AuthorEntityId = 9,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var result = await controller.GetComments(CommentTargetType.Artist, 9);

        var response = Assert.IsType<CommentsResponse>(Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result).Value);
        var dto = Assert.Single(response.Comments);

        Assert.Equal("artist", dto.AuthorEntityType);
        Assert.Equal(9, dto.AuthorEntityId);
        Assert.Equal("manishb", dto.AuthorDisplayName);
        Assert.Null(dto.AuthorImage); // Must NOT fall back to the base user's image.
    }

    [Fact]
    public async Task GetComments_UserContext_ReturnsUserNameAndImage()
    {
        await using var context = CreateContext(nameof(GetComments_UserContext_ReturnsUserNameAndImage));

        context.Set<NextAuthUser>().Add(new NextAuthUser { Id = 2, Name = "Jane", Image = "jane.jpg" });
        context.Comments.Add(new Comment
        {
            Id = 2,
            TargetType = CommentTargetType.Blog,
            TargetId = 5,
            UserId = 2,
            Content = "Nice piece",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var result = await controller.GetComments(CommentTargetType.Blog, 5);

        var response = Assert.IsType<CommentsResponse>(Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result).Value);
        var dto = Assert.Single(response.Comments);

        Assert.Equal("user", dto.AuthorEntityType);
        Assert.Null(dto.AuthorEntityId);
        Assert.Equal("Jane", dto.AuthorDisplayName);
        Assert.Equal("jane.jpg", dto.AuthorImage);
    }

    [Fact]
    public async Task GetComments_NestedArtistReply_ReturnsArtistTitleAndImage()
    {
        await using var context = CreateContext(nameof(GetComments_NestedArtistReply_ReturnsArtistTitleAndImage));

        context.Set<NextAuthUser>().Add(new NextAuthUser { Id = 1, Name = "BaseUser", Image = "user.jpg" });
        context.Pictures.Add(new Picture { PictureID = 20, URL = "https://cdn.example.com/artist-8.jpg" });
        context.Artists.Add(new Artist { ArtistID = 8, Title = "ArtStudio8", Path = "art-studio-8", Applied = DateTime.UtcNow, Since = DateTime.UtcNow, ProfilePicID = 20 });
        context.Comments.Add(new Comment
        {
            Id = 100,
            TargetType = CommentTargetType.Listing,
            TargetId = 3,
            UserId = 1,
            Content = "Top-level comment",
            CreatedAt = DateTime.UtcNow
        });
        context.Comments.Add(new Comment
        {
            Id = 101,
            ParentCommentId = 100,
            TargetType = CommentTargetType.Listing,
            TargetId = 3,
            UserId = 1,
            Content = "Reply as artist",
            AuthorContextId = "artist-8",
            AuthorEntityType = "artist",
            AuthorEntityId = 8,
            CreatedAt = DateTime.UtcNow.AddMinutes(1)
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var result = await controller.GetComments(CommentTargetType.Listing, 3, includeReplies: true);

        var response = Assert.IsType<CommentsResponse>(Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result).Value);
        var topLevel = Assert.Single(response.Comments);
        var reply = Assert.Single(topLevel.Replies!);

        Assert.Equal("artist", reply.AuthorEntityType);
        Assert.Equal("ArtStudio8", reply.AuthorDisplayName);
        Assert.Equal("https://cdn.example.com/artist-8.jpg", reply.AuthorImage);
    }

    [Fact]
    public async Task GetComments_MultipleCommentsByDifferentArtists_ResolveAuthorsInOneBatchEach()
    {
        await using var context = CreateContext(nameof(GetComments_MultipleCommentsByDifferentArtists_ResolveAuthorsInOneBatchEach));

        context.Set<NextAuthUser>().AddRange(
            new NextAuthUser { Id = 1, Name = "UserOne", Image = "u1.jpg" },
            new NextAuthUser { Id = 2, Name = "UserTwo", Image = "u2.jpg" });

        context.Pictures.AddRange(
            new Picture { PictureID = 10, URL = "https://cdn/artist-10.jpg" },
            new Picture { PictureID = 11, URL = "https://cdn/artist-11.jpg" },
            new Picture { PictureID = 12, URL = "https://cdn/artist-12.jpg" });

        context.Artists.AddRange(
            new Artist { ArtistID = 10, Title = "Artist10", Path = "artist-10", Applied = DateTime.UtcNow, Since = DateTime.UtcNow, ProfilePicID = 10 },
            new Artist { ArtistID = 11, Title = "Artist11", Path = "artist-11", Applied = DateTime.UtcNow, Since = DateTime.UtcNow, ProfilePicID = 11 },
            new Artist { ArtistID = 12, Title = "Artist12", Path = "artist-12", Applied = DateTime.UtcNow, Since = DateTime.UtcNow, ProfilePicID = 12 });

        for (var i = 0; i < 6; i++)
        {
            var artistId = 10 + (i % 3);
            context.Comments.Add(new Comment
            {
                Id = 200 + i,
                TargetType = CommentTargetType.Blog,
                TargetId = 99,
                UserId = 1,
                Content = $"Comment {i}",
                AuthorContextId = $"artist-{artistId}",
                AuthorEntityType = "artist",
                AuthorEntityId = artistId,
                CreatedAt = DateTime.UtcNow.AddSeconds(i)
            });
        }

        // Two extra base-user comments (older comments with no selected profile context).
        context.Comments.Add(new Comment { Id = 210, TargetType = CommentTargetType.Blog, TargetId = 99, UserId = 1, Content = "As myself", CreatedAt = DateTime.UtcNow });
        context.Comments.Add(new Comment { Id = 211, TargetType = CommentTargetType.Blog, TargetId = 99, UserId = 2, Content = "Also myself", CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var countingContext = new CountingTAGDBContext(new DbContextOptionsBuilder<TAGDBContext>()
            .UseInMemoryDatabase(nameof(GetComments_MultipleCommentsByDifferentArtists_ResolveAuthorsInOneBatchEach))
            .Options);
        var controller = CreateController(countingContext);

        var result = await controller.GetComments(CommentTargetType.Blog, 99, pageSize: 20);

        var response = Assert.IsType<CommentsResponse>(Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result).Value);
        Assert.Equal(8, response.Comments.Count);

        // Every artist-authored comment resolved correctly...
        foreach (var dto in response.Comments.Where(c => c.AuthorEntityId.HasValue))
        {
            Assert.Equal("artist", dto.AuthorEntityType);
            Assert.Equal($"Artist{dto.AuthorEntityId}", dto.AuthorDisplayName);
            Assert.Equal($"https://cdn/artist-{dto.AuthorEntityId}.jpg", dto.AuthorImage);
        }

        // ...but resolving all 3 distinct artists and both users took exactly one batched
        // lookup each, not one query per comment (which would be 6 artist lookups here).
        Assert.Equal(1, countingContext.ArtistSetAccessCount);
        Assert.Equal(1, countingContext.NextAuthUserSetAccessCount);
    }

    // Counts how many times each DbSet is requested, to prove author resolution is batched
    // (one access per GetComments call) rather than issued once per comment (N+1).
    private sealed class CountingTAGDBContext : TAGDBContext
    {
        public int ArtistSetAccessCount { get; private set; }
        public int NextAuthUserSetAccessCount { get; private set; }

        public CountingTAGDBContext(DbContextOptions<TAGDBContext> options)
            : base(options)
        {
        }

        public override DbSet<TEntity> Set<TEntity>()
        {
            if (typeof(TEntity) == typeof(Artist))
            {
                ArtistSetAccessCount++;
            }
            else if (typeof(TEntity) == typeof(NextAuthUser))
            {
                NextAuthUserSetAccessCount++;
            }

            return base.Set<TEntity>();
        }
    }
}
