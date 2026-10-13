// <copyright file="BlogController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BlogController : ControllerBase
    {
        private readonly TAGDBContext _context;
        private readonly ILogger<BlogController> _logger;
        private static readonly Regex ValidPathRegex = new(@"^[a-z0-9](?:[a-z0-9\-]*[a-z0-9])?$", RegexOptions.Compiled);

        public BlogController(TAGDBContext context, ILogger<BlogController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<Blog>>> GetBlogs()
        {
            if (_context.Blogs == null)
            {
                return NotFound();
            }
            var blogs = await _context.Blogs
                .Where(b => b.StatusID == BlogStatus.Published)
                .Include(b => b.User)
                .Include(b => b.Gallery!)
                    .ThenInclude(g => g.GalleryItems)
                    .ThenInclude(gi => gi.Picture)
                .Include(b => b.Gallery!)
                    .ThenInclude(g => g.GalleryItems)
                    .ThenInclude(gi => gi.Video)
                .ToListAsync();

            return Ok(blogs.Select(MapBlogForApi));
        }

        [HttpGet("published")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<PublishedBlogResponse>>> GetPublishedBlogs()
        {
            if (_context.Blogs == null)
            {
                return NotFound();
            }

            var blogs = await _context.Blogs
                .AsNoTracking()
                .Where(b => b.StatusID == BlogStatus.Published)
                .Include(b => b.User)
                    .ThenInclude(user => user.ProfilePic)
                .Include(b => b.Gallery!)
                    .ThenInclude(g => g.GalleryItems)
                    .ThenInclude(gi => gi.Picture)
                .Include(b => b.Gallery!)
                    .ThenInclude(g => g.GalleryItems)
                    .ThenInclude(gi => gi.Video)
                .ToListAsync();

            return Ok(blogs.Select(MapPublishedBlogForApi));
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<Blog>> GetBlog(int id)
        {
            if (_context.Blogs == null)
            {
                return NotFound();
            }
            var blog = await _context.Blogs
                .Include(b => b.User)
                .Include(b => b.Gallery!)
                    .ThenInclude(g => g.GalleryItems)
                    .ThenInclude(gi => gi.Picture)
                .Include(b => b.Gallery!)
                    .ThenInclude(g => g.GalleryItems)
                    .ThenInclude(gi => gi.Video)
                .FirstOrDefaultAsync(b => b.BlogID == id && b.StatusID != BlogStatus.ModerationBlocked);

            if (blog == null)
            {
                return NotFound();
            }

            return Ok(MapBlogForApi(blog));
        }

        [HttpGet("path/{path}")]
        [AllowAnonymous]
        public async Task<ActionResult<Blog>> GetBlogByPath(string path)
        {
            if (_context.Blogs == null)
            {
                return NotFound();
            }
            var blog = await _context.Blogs
                .Include(b => b.User)
                .Include(b => b.Gallery!)
                    .ThenInclude(g => g.GalleryItems)
                    .ThenInclude(gi => gi.Picture)
                .Include(b => b.Gallery!)
                    .ThenInclude(g => g.GalleryItems)
                    .ThenInclude(gi => gi.Video)
                .FirstOrDefaultAsync(b => b.Path == path && b.StatusID == BlogStatus.Published);

            if (blog == null)
            {
                return NotFound();
            }

            return Ok(MapBlogForApi(blog));
        }

        private static string NormalizeResourceUrl(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        private async Task<Gallery> EnsureBlogGalleryAsync(Blog blog)
        {
            if (blog.GalleryID.HasValue)
            {
                var existingGallery = await _context.Galleries
                    .FirstOrDefaultAsync(gallery => gallery.GalleryID == blog.GalleryID.Value)
                    .ConfigureAwait(false);

                if (existingGallery != null)
                {
                    return existingGallery;
                }
            }

            var now = DateTime.UtcNow;
            var gallery = new Gallery
            {
                ScopeType = "blog",
                ScopeEntityID = blog.BlogID,
                OwnerUserID = blog.UserID,
                IsPrimary = true,
                Title = $"Blog {blog.BlogID} Gallery",
                Created = now,
                Updated = now,
            };

            _context.Galleries.Add(gallery);
            await _context.SaveChangesAsync().ConfigureAwait(false);

            blog.GalleryID = gallery.GalleryID;
            blog.Modified = now;
            await _context.SaveChangesAsync().ConfigureAwait(false);

            return gallery;
        }

        [HttpPost("{id}/gallery/video")]
        public async Task<ActionResult<GalleryItem>> PostBlogGalleryVideo(int id, [FromBody] BlogVideoUpsertRequest request)
        {
            var blog = await _context.Blogs
                .FirstOrDefaultAsync(candidate => candidate.BlogID == id)
                .ConfigureAwait(false);

            if (blog == null)
            {
                return NotFound();
            }

            var normalizedEmbed = NormalizeResourceUrl(request?.EmbedURL);
            if (string.IsNullOrWhiteSpace(normalizedEmbed))
            {
                return BadRequest("EmbedURL is required.");
            }

            var normalizedSourceUrl = NormalizeResourceUrl(request?.URL);
            var now = DateTime.UtcNow;

            var video = await _context.Videos
                .FirstOrDefaultAsync(candidate =>
                    candidate.NormalizedEmbedURL == normalizedEmbed ||
                    candidate.EmbedURL == request!.EmbedURL ||
                    (!string.IsNullOrWhiteSpace(normalizedSourceUrl) && candidate.URL != null && candidate.URL.ToLower() == normalizedSourceUrl))
                .ConfigureAwait(false);

            if (video == null)
            {
                video = new Video
                {
                    EmbedURL = request!.EmbedURL!.Trim(),
                    URL = request.URL,
                    ThumbnailURL = request.ThumbnailURL,
                    Title = request.Title,
                    Byline = request.Byline,
                    Description = request.Description,
                    Provider = string.IsNullOrWhiteSpace(request.Provider) ? "vimeo" : request.Provider.Trim().ToLowerInvariant(),
                    ProviderVideoID = request.ProviderVideoID,
                    NormalizedEmbedURL = normalizedEmbed,
                    Created = now,
                    Updated = now,
                };

                _context.Videos.Add(video);
                await _context.SaveChangesAsync().ConfigureAwait(false);
            }
            else
            {
                video.URL = string.IsNullOrWhiteSpace(request?.URL) ? video.URL : request!.URL;
                video.ThumbnailURL = string.IsNullOrWhiteSpace(request?.ThumbnailURL) ? video.ThumbnailURL : request!.ThumbnailURL;
                video.Title = string.IsNullOrWhiteSpace(request?.Title) ? video.Title : request!.Title;
                video.Byline = string.IsNullOrWhiteSpace(request?.Byline) ? video.Byline : request!.Byline;
                video.Description = string.IsNullOrWhiteSpace(request?.Description) ? video.Description : request!.Description;
                video.Provider = string.IsNullOrWhiteSpace(request?.Provider) ? video.Provider : request!.Provider!.Trim().ToLowerInvariant();
                video.ProviderVideoID = string.IsNullOrWhiteSpace(request?.ProviderVideoID) ? video.ProviderVideoID : request!.ProviderVideoID;
                video.NormalizedEmbedURL = normalizedEmbed;
                video.Updated = now;
                await _context.SaveChangesAsync().ConfigureAwait(false);
            }

            var gallery = await EnsureBlogGalleryAsync(blog).ConfigureAwait(false);

            var existingGalleryItem = await _context.GalleryItems
                .FirstOrDefaultAsync(item => item.GalleryID == gallery.GalleryID && item.VideoID == video.VideoID)
                .ConfigureAwait(false);

            if (existingGalleryItem == null)
            {
                var nextSortOrder = await _context.GalleryItems
                    .Where(item => item.GalleryID == gallery.GalleryID)
                    .Select(item => (int?)item.SortOrder)
                    .MaxAsync()
                    .ConfigureAwait(false) ?? -1;

                existingGalleryItem = new GalleryItem
                {
                    GalleryID = gallery.GalleryID,
                    VideoID = video.VideoID,
                    SortOrder = nextSortOrder + 1,
                    AddedByUserID = blog.UserID,
                    Created = now,
                };

                _context.GalleryItems.Add(existingGalleryItem);
                gallery.Updated = now;
                await _context.SaveChangesAsync().ConfigureAwait(false);
            }

            // Reload the item with navigation properties
            var reloaded = await _context.GalleryItems
                .Include(gi => gi.Video)
                .FirstAsync(gi => gi.GalleryItemID == existingGalleryItem.GalleryItemID)
                .ConfigureAwait(false);

            return Ok(reloaded);
        }

        [HttpPut("{id}/gallery/order")]
        public async Task<ActionResult<IEnumerable<GalleryItem>>> PutBlogGalleryOrder(int id, [FromBody] BlogGalleryOrderRequest request)
        {
            var blog = await _context.Blogs
                .FirstOrDefaultAsync(candidate => candidate.BlogID == id)
                .ConfigureAwait(false);

            if (blog == null)
            {
                return NotFound();
            }

            var orderedItems = request?.Items ?? new List<BlogGalleryOrderItemDto>();
            var gallery = await EnsureBlogGalleryAsync(blog).ConfigureAwait(false);
            var existingGalleryItems = await _context.GalleryItems
                .Where(item => item.GalleryID == gallery.GalleryID)
                .ToListAsync()
                .ConfigureAwait(false);

            var captionByPictureId = existingGalleryItems
                .Where(item => item.PictureID.HasValue)
                .GroupBy(item => item.PictureID!.Value)
                .ToDictionary(group => group.Key, group => group.First().CaptionOverride);

            var captionByVideoId = existingGalleryItems
                .Where(item => item.VideoID.HasValue)
                .GroupBy(item => item.VideoID!.Value)
                .ToDictionary(group => group.Key, group => group.First().CaptionOverride);

            var resolvedItems = new List<(int? PictureID, int? VideoID, int SortOrder)>();

            for (var index = 0; index < orderedItems.Count; index++)
            {
                var requestedItem = orderedItems[index];
                var mediaType = (requestedItem.MediaType ?? string.Empty).Trim().ToLowerInvariant();

                if (mediaType == "video")
                {
                    Video? resolvedVideo = null;
                    if (requestedItem.VideoID.HasValue)
                    {
                        resolvedVideo = await _context.Videos
                            .FirstOrDefaultAsync(video => video.VideoID == requestedItem.VideoID.Value)
                            .ConfigureAwait(false);
                    }

                    if (resolvedVideo == null)
                    {
                        var normalizedVideoUrl = NormalizeResourceUrl(requestedItem.Url);
                        var normalizedEmbedUrl = NormalizeResourceUrl(requestedItem.EmbedURL);

                        resolvedVideo = await _context.Videos
                            .FirstOrDefaultAsync(video =>
                                (!string.IsNullOrWhiteSpace(normalizedEmbedUrl) && video.NormalizedEmbedURL == normalizedEmbedUrl) ||
                                (!string.IsNullOrWhiteSpace(normalizedVideoUrl) && video.URL != null && video.URL.ToLower() == normalizedVideoUrl))
                            .ConfigureAwait(false);
                    }

                    if (resolvedVideo == null)
                    {
                        return BadRequest($"Unable to resolve video at gallery index {index}.");
                    }

                    resolvedItems.Add((null, resolvedVideo.VideoID, index));
                    continue;
                }

                Picture? resolvedPicture = null;
                if (requestedItem.PictureID.HasValue)
                {
                    resolvedPicture = await _context.Pictures
                        .FirstOrDefaultAsync(picture => picture.PictureID == requestedItem.PictureID.Value)
                        .ConfigureAwait(false);
                }

                if (resolvedPicture == null)
                {
                    var normalizedPictureUrl = NormalizeResourceUrl(requestedItem.Url);
                    resolvedPicture = await _context.Pictures
                        .FirstOrDefaultAsync(picture =>
                            picture.NormalizedURL == normalizedPictureUrl ||
                            picture.URL == requestedItem.Url)
                        .ConfigureAwait(false);
                }

                if (resolvedPicture == null)
                {
                    return BadRequest($"Unable to resolve picture at gallery index {index}.");
                }

                resolvedItems.Add((resolvedPicture.PictureID, null, index));
            }

            _context.GalleryItems.RemoveRange(existingGalleryItems);
            await _context.SaveChangesAsync().ConfigureAwait(false);

            var now = DateTime.UtcNow;
            var replacementItems = resolvedItems.Select(item => new GalleryItem
            {
                GalleryID = gallery.GalleryID,
                PictureID = item.PictureID,
                VideoID = item.VideoID,
                SortOrder = item.SortOrder,
                CaptionOverride = item.PictureID.HasValue
                    ? captionByPictureId.GetValueOrDefault(item.PictureID.Value)
                    : (item.VideoID.HasValue ? captionByVideoId.GetValueOrDefault(item.VideoID.Value) : null),
                AddedByUserID = blog.UserID,
                Created = now,
            }).ToList();

            _context.GalleryItems.AddRange(replacementItems);
            gallery.Updated = now;
            await _context.SaveChangesAsync().ConfigureAwait(false);

            // Reload gallery items with related Picture/Video data
            var refreshedItems = await _context.GalleryItems
                .Where(gi => gi.GalleryID == gallery.GalleryID)
                .Include(gi => gi.Picture)
                .Include(gi => gi.Video)
                .OrderBy(gi => gi.SortOrder)
                .ToListAsync()
                .ConfigureAwait(false);

            return Ok(refreshedItems);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutBlog(int id, [FromBody] Blog dto)
        {
            // 1. Basic Validation
            if (id != dto.BlogID)
                return BadRequest("ID Mismatch between URL and Body.");

            if (!ValidPathRegex.IsMatch(dto.Path))
                return BadRequest("Invalid path format.");

            // 2. Fetch the EXISTING record from the DB
            // This is safer than _context.Entry(blog).State = Modified
            var blog = await _context.Blogs.FindAsync(id);
            if (blog == null) return NotFound();

            // 3. Unique Path Check (Only if Path is changing)
            if (dto.Path != blog.Path)
            {
                if (!await IsPathUniqueAsync(dto.Path, id))
                    return Conflict("Path is already in use.");
                blog.Path = dto.Path;
            }

            // 4. Map only the allowed fields
            blog.Title = dto.Title;
            blog.Body = dto.Body;
            blog.Body_Plaintext = dto.Body_Plaintext;
            blog.Byline = dto.Byline;
            blog.UserID = dto.UserID;
            blog.Modified = DateTime.UtcNow; // Set on server

            // 5. Save Changes
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BlogExists(id)) return NotFound();
                throw;
            }

            return NoContent(); // 204 is the standard success for PUT
        }

        [HttpPost]
        public async Task<ActionResult<Blog>> PostBlog(Blog blog)
        {
            if (_context.Blogs == null)
            {
                return Problem("Entity set 'TAGDBContext.Blogs' is null.");
            }

            // Set creation timestamp
            blog.Created = DateTime.UtcNow;

            // Generate a unique path if not provided
            if (string.IsNullOrWhiteSpace(blog.Path))
            {
                blog.Path = await GenerateUniquePathAsync(blog.Title);
            }

            // Ensure the path is valid and unique
            if (!ValidPathRegex.IsMatch(blog.Path))
            {
                return BadRequest("Invalid path format.");
            }

            if (!await IsPathUniqueAsync(blog.Path))
            {
                return Conflict("Path is already in use.");
            }

            _context.Blogs.Add(blog);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetBlog", new { id = blog.BlogID }, blog);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBlog(int id)
        {
            if (_context.Blogs == null)
            {
                return NotFound();
            }
            var blog = await _context.Blogs.FindAsync(id);
            if (blog == null)
            {
                return NotFound();
            }

            _context.Blogs.Remove(blog);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("credit-roles")]
        public async Task<ActionResult<IEnumerable<CreditRole>>> GetCreditRoles()
        {
            return await _context.CreditRoles
                .Where(role => role.IsActive)
                .OrderBy(role => role.DisplayOrder)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        [HttpGet("{id}/credits")]
        public async Task<ActionResult<IEnumerable<BlogCreditDto>>> GetBlogCredits(int id)
        {
            if (!BlogExists(id))
            {
                return NotFound();
            }

            var credits = await (
                from credit in _context.MediaCredits
                join role in _context.CreditRoles on credit.CreditRoleID equals role.CreditRoleID
                join party in _context.CreditParties on credit.CreditPartyID equals party.CreditPartyID
                where credit.BlogID == id
                orderby credit.SortOrder, role.DisplayOrder, role.Label
                select new BlogCreditDto
                {
                    MediaCreditID = credit.MediaCreditID,
                    CreditRoleID = role.CreditRoleID,
                    Role = role.Label,
                    CreditPartyID = party.CreditPartyID,
                    DisplayName = party.DisplayName,
                    UserID = party.UserID,
                    ArtistID = party.ArtistID,
                    ExternalURL = party.ExternalURL,
                    BioNote = party.BioNote,
                    CreditText = credit.CreditText,
                    SortOrder = credit.SortOrder,
                })
                .ToListAsync()
                .ConfigureAwait(false);

            return Ok(credits);
        }

        [HttpPut("{id}/credits")]
        public async Task<ActionResult<IEnumerable<BlogCreditDto>>> PutBlogCredits(int id, [FromBody] BlogCreditsUpsertRequest request)
        {
            if (!BlogExists(id))
            {
                return NotFound();
            }

            var submittedCredits = request?.Credits ?? new List<BlogCreditInputDto>();

            foreach (var submitted in submittedCredits)
            {
                if (submitted.CreditRoleID <= 0)
                {
                    return BadRequest("Each credit must include a valid CreditRoleID.");
                }

                if (submitted.CreditPartyID.HasValue)
                {
                    continue;
                }

                if (submitted.Party == null)
                {
                    return BadRequest("Each credit must include either CreditPartyID or Party details.");
                }

                var hasLinkedIdentity = submitted.Party.UserID.HasValue || submitted.Party.ArtistID.HasValue;
                var hasDisplayName = !string.IsNullOrWhiteSpace(submitted.Party.DisplayName);
                if (!hasLinkedIdentity && !hasDisplayName)
                {
                    return BadRequest("Each Party requires a linked identity or a display name.");
                }
            }

            var existingCredits = await _context.MediaCredits
                .Where(credit => credit.BlogID == id)
                .ToListAsync()
                .ConfigureAwait(false);

            _context.MediaCredits.RemoveRange(existingCredits);

            foreach (var submitted in submittedCredits.OrderBy(credit => credit.SortOrder))
            {
                var creditPartyId = submitted.CreditPartyID;

                if (!creditPartyId.HasValue && submitted.Party != null)
                {
                    var party = new CreditParty
                    {
                        UserID = submitted.Party.UserID,
                        ArtistID = submitted.Party.ArtistID,
                        DisplayName = submitted.Party.DisplayName,
                        ExternalURL = submitted.Party.ExternalURL,
                        BioNote = submitted.Party.BioNote,
                        Created = DateTime.UtcNow,
                    };

                    _context.CreditParties.Add(party);
                    await _context.SaveChangesAsync().ConfigureAwait(false);
                    creditPartyId = party.CreditPartyID;
                }

                if (!creditPartyId.HasValue)
                {
                    return BadRequest("Unable to resolve CreditPartyID for one or more credits.");
                }

                _context.MediaCredits.Add(new MediaCredit
                {
                    BlogID = id,
                    CreditRoleID = submitted.CreditRoleID,
                    CreditPartyID = creditPartyId.Value,
                    CreditText = submitted.CreditText,
                    SortOrder = submitted.SortOrder,
                });
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);

            return await GetBlogCredits(id).ConfigureAwait(false);
        }


        /// <summary>
        /// Reserves a slug for a new blog by creating a minimal blog record
        /// </summary>
        /// <param name="request">The blog slug reservation request</param>
        /// <returns>The created blog with ID and slug</returns>
        [HttpPost("reserve-slug")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<BlogSlugReservationResponse>> ReserveBlogSlug([FromBody] BlogSlugReservationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate the slug format
            if (!ValidPathRegex.IsMatch(request.Slug))
            {
                return BadRequest("Invalid slug format.");
            }

            // Ensure the slug is unique
            if (!await IsPathUniqueAsync(request.Slug))
            {
                return Conflict("Slug is already in use.");
            }

            // Create a new blog with minimal required fields
            var blog = new Blog
            {
                Path = request.Slug,
                Title = request.Title ?? request.Slug, // Default to slug if title isn't provided
                Byline = request.Byline ?? string.Empty, // Required field
                Body = request.Body ?? string.Empty,    // Required field
                UserID = request.UserID,
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow
            };

            try
            {
                _context.Blogs.Add(blog);
                await _context.SaveChangesAsync();

                // Return the reserved blog object with its ID
                var response = new BlogSlugReservationResponse
                {
                    BlogID = blog.BlogID,
                    Path = blog.Path,
                    Title = blog.Title
                };

                return CreatedAtAction(nameof(GetBlog), new { id = blog.BlogID }, response);
            }
            catch (DbUpdateException ex)
            {
                // Log the exception
                _logger.LogError(ex, "Failed to reserve blog slug: {Slug}", request.Slug);

                // Handle unique constraint violations or other DB errors
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Failed to reserve slug", error = ex.InnerException?.Message ?? ex.Message });
            }
        }

        /// <summary>
        /// Updates only the slug/path for an existing blog
        /// </summary>
        /// <param name="id">The blog ID</param>
        /// <param name="request">The slug update request</param>
        /// <returns>The updated blog</returns>
        [HttpPatch("{id}/update-slug")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<BlogSlugReservationResponse>> UpdateBlogSlug(int id, [FromBody] BlogSlugUpdateRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var blog = await _context.Blogs.FindAsync(id);
            if (blog == null)
            {
                return NotFound(new { message = $"Blog with ID {id} not found" });
            }

            // If no change, return success immediately
            if (blog.Path == request.Slug)
            {
                return Ok(new BlogSlugReservationResponse
                {
                    BlogID = blog.BlogID,
                    Path = blog.Path,
                    Title = blog.Title
                });
            }

            // Validate the slug format
            if (!ValidPathRegex.IsMatch(request.Slug))
            {
                return BadRequest("Invalid slug format.");
            }

            // Ensure the slug is unique
            if (!await IsPathUniqueAsync(request.Slug, id))
            {
                return Conflict("Slug is already in use.");
            }

            // Update the slug
            blog.Path = request.Slug;
            blog.Modified = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();

                // Return the updated blog
                var response = new BlogSlugReservationResponse
                {
                    BlogID = blog.BlogID,
                    Path = blog.Path,
                    Title = blog.Title
                };

                return Ok(response);
            }
            catch (DbUpdateException ex)
            {
                // Log the exception
                _logger.LogError(ex, "Failed to update blog slug: {Slug}", request.Slug);

                // Handle unique constraint violations or other DB errors
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Failed to update slug", error = ex.InnerException?.Message ?? ex.Message });
            }
        }

        private bool BlogExists(int id)
        {
            return (_context.Blogs?.Any(e => e.BlogID == id)).GetValueOrDefault();
        }

        private static object MapBlogForApi(Blog blog)
        {
            return new
            {
                blog.BlogID,
                Body = CoalescePlaintext(blog.Body_Plaintext, blog.Body),
                BodyRichtext = blog.Body,
                Byline = CoalescePlaintext(blog.Byline_Plaintext, blog.Byline),
                BylineRichtext = blog.Byline,
                blog.Created,
                blog.Modified,
                blog.Path,
                Title = CoalescePlaintext(blog.Title_Plaintext, blog.Title),
                TitleRichtext = blog.Title,
                blog.UserID,
                blog.GalleryID,
                blog.CoverPicID,
                blog.User,
                blog.Gallery,
                blog.CoverPic,
                blog.StatusID,
                Status = blog.StatusID.ToString(),
            };
        }

        private static PublishedBlogResponse MapPublishedBlogForApi(Blog blog)
        {
            return new PublishedBlogResponse
            {
                BlogID = blog.BlogID,
                Body = CoalescePlaintext(blog.Body_Plaintext, blog.Body),
                BodyRichtext = blog.Body,
                Byline = CoalescePlaintext(blog.Byline_Plaintext, blog.Byline),
                BylineRichtext = blog.Byline,
                Created = blog.Created,
                Modified = blog.Modified,
                Path = blog.Path,
                Title = CoalescePlaintext(blog.Title_Plaintext, blog.Title),
                TitleRichtext = blog.Title,
                UserID = blog.UserID,
                GalleryID = blog.GalleryID,
                CoverPicID = blog.CoverPicID,
                User = blog.User == null
                    ? null
                    : new UserPublicSummaryResponse
                    {
                        UserID = blog.User.UserID,
                        Username = blog.User.Username ?? string.Empty,
                        PreferredName = string.IsNullOrWhiteSpace(blog.User.PreferredName)
                            ? blog.User.Username ?? string.Empty
                            : blog.User.PreferredName,
                        ProfilePic = blog.User.ProfilePic?.URL,
                    },
                Gallery = blog.Gallery,
                CoverPic = blog.CoverPic,
                StatusID = blog.StatusID,
                Status = blog.StatusID.ToString(),
            };
        }

        private static string? CoalescePlaintext(string? plaintext, string? richtext)
        {
            return !string.IsNullOrWhiteSpace(plaintext)
                ? plaintext
                : StripHtmlToPlaintext(richtext);
        }

        private static string? StripHtmlToPlaintext(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var withoutScripts = Regex.Replace(value, "(?is)<(script|style)[^>]*>.*?</\\1>", " ");
            var withBreaks = Regex.Replace(withoutScripts, "(?i)<br\\s*/?>", "\n");
            var withBlockBreaks = Regex.Replace(withBreaks, "(?i)</(p|div|li|h[1-6]|tr|section|article|blockquote)>", "\n");
            var noTags = Regex.Replace(withBlockBreaks, "(?is)<[^>]+>", " ");
            var normalized = Regex.Replace(noTags, @"[ \t\r]+", " ");
            normalized = Regex.Replace(normalized, @"\n\s*\n+", "\n");
            var decoded = normalized
                .Replace("&nbsp;", " ")
                .Replace("&amp;", "&")
                .Replace("&quot;", "\"")
                .Replace("&#39;", "'")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">");

            var trimmed = decoded.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        // Add a helper method to generate paths if needed
        private async Task<string> GenerateUniquePathAsync(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Title cannot be empty", nameof(title));
            }

            // Convert title to path format
            var path = title.ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("_", "-")
                .Replace(".", "-")
                .Replace("&", "and");

            // Remove any characters not allowed in a path
            path = Regex.Replace(path, @"[^a-z0-9\-]", string.Empty);

            // Replace multiple hyphens with a single hyphen
            path = Regex.Replace(path, @"\-{2,}", "-");

            // Trim hyphens from start and end
            path = path.Trim('-');

            // If path is empty after processing, use a default
            if (string.IsNullOrWhiteSpace(path))
            {
                path = $"blog-{Guid.NewGuid().ToString().Substring(0, 8)}";
            }

            // Make sure it's unique
            var counter = 0;
            var originalPath = path;

            while (await _context.Blogs.AnyAsync(b => b.Path == path))
            {
                counter++;
                path = $"{originalPath}-{counter}";
            }

            return path;
        }

        // Add a helper method to validate paths
        private async Task<bool> IsPathUniqueAsync(string path, int? blogId = null)
        {
            var query = _context.Blogs.AsQueryable();

            if (blogId.HasValue)
            {
                query = query.Where(b => b.BlogID != blogId.Value);
            }

            return !await query.AnyAsync(b => b.Path == path);
        }
    }

    // Request models for slug operations
    public class BlogSlugReservationRequest
    {
        [Required]
        [StringLength(255, MinimumLength = 1)]
        public string Slug { get; set; }

        [StringLength(1000)]
        public string Title { get; set; }

        [StringLength(1000)]
        public string Byline { get; set; }

        public string Body { get; set; }

        [Required]
        public int UserID { get; set; }
    }

    public class BlogSlugUpdateRequest
    {
        [Required]
        [StringLength(255, MinimumLength = 1)]
        public string Slug { get; set; }
    }

    public class BlogSlugReservationResponse
    {
        public int BlogID { get; set; }
        public string Path { get; set; }
        public string Title { get; set; }
    }

    public class BlogCreditsUpsertRequest
    {
        public List<BlogCreditInputDto> Credits { get; set; } = new();
    }

    public class BlogCreditInputDto
    {
        public int? CreditPartyID { get; set; }
        public int CreditRoleID { get; set; }
        public int SortOrder { get; set; }
        public string? CreditText { get; set; }
        public BlogCreditPartyInputDto? Party { get; set; }
    }

    public class BlogCreditPartyInputDto
    {
        public int? UserID { get; set; }
        public int? ArtistID { get; set; }
        public string? DisplayName { get; set; }
        public string? ExternalURL { get; set; }
        public string? BioNote { get; set; }
    }

    public class BlogCreditDto
    {
        public int MediaCreditID { get; set; }
        public int CreditRoleID { get; set; }
        public string Role { get; set; } = string.Empty;
        public int CreditPartyID { get; set; }
        public string? DisplayName { get; set; }
        public int? UserID { get; set; }
        public int? ArtistID { get; set; }
        public string? ExternalURL { get; set; }
        public string? BioNote { get; set; }
        public string? CreditText { get; set; }
        public int SortOrder { get; set; }
    }

    public class BlogVideoUpsertRequest
    {
        public string? URL { get; set; }

        public string? EmbedURL { get; set; }

        public string? ThumbnailURL { get; set; }

        public string? Provider { get; set; }

        public string? ProviderVideoID { get; set; }

        public string? Title { get; set; }

        public string? Byline { get; set; }

        public string? Description { get; set; }
    }

    public class BlogGalleryOrderRequest
    {
        public List<BlogGalleryOrderItemDto> Items { get; set; } = new();
    }

    public class BlogGalleryOrderItemDto
    {
        public string? MediaType { get; set; }

        public int? PictureID { get; set; }

        public int? VideoID { get; set; }

        public string? Url { get; set; }

        public string? EmbedURL { get; set; }
    }

    public class PublishedBlogResponse
    {
        public int BlogID { get; set; }

        public string? Body { get; set; }

        public string BodyRichtext { get; set; }

        public string? Byline { get; set; }

        public string BylineRichtext { get; set; }

        public DateTime Created { get; set; }

        public DateTime? Modified { get; set; }

        public string Path { get; set; }

        public string? Title { get; set; }

        public string TitleRichtext { get; set; }

        public int UserID { get; set; }

        public int? GalleryID { get; set; }

        public int? CoverPicID { get; set; }

        public UserPublicSummaryResponse? User { get; set; }

        public Gallery? Gallery { get; set; }

        public Picture? CoverPic { get; set; }

        public BlogStatus StatusID { get; set; }

        public string Status { get; set; }
    }
}
