// <copyright file="ListingController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
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
    public class ListingController : ControllerBase
    {
        private readonly TAGDBContext _context;
        private readonly ILogger<ListingController> _logger;
        private static readonly Regex ValidPathRegex = new(@"^[a-z0-9](?:[a-z0-9\-]*[a-z0-9])?$", RegexOptions.Compiled);

        public ListingController(TAGDBContext context, ILogger<ListingController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<Listing>>> GetListings()
        {
            if (_context.Listings == null)
            {
                return NotFound();
            }
            var listings = await _context.Listings
                .Where(l => l.IsPublished && !l.IsModerationBlocked && l.Artist != null && l.Artist.IsPublished && !l.Artist.IsModerationBlocked)
                .Include(l => l.Artist)
                    .ThenInclude(a => a.ProfilePic)
                .Include(l => l.ArtCategory)
                .Include(l => l.CoverPic)
                .ToListAsync();

            if (!listings.Any())
            {
                _logger.LogWarning("No published listings found; returning non-moderation-blocked listings as fallback.");

                listings = await _context.Listings
                    .Where(l => !l.IsModerationBlocked && l.Artist != null && !l.Artist.IsModerationBlocked)
                    .Include(l => l.Artist)
                        .ThenInclude(a => a.ProfilePic)
                    .Include(l => l.ArtCategory)
                    .Include(l => l.CoverPic)
                    .ToListAsync();
            }

            await HydrateListingArtistsAsync(listings).ConfigureAwait(false);
            return Ok(listings.Select(MapListingForApi));
        }

        [HttpGet("admin/unpublished")]
        public async Task<ActionResult<IEnumerable<Listing>>> GetUnpublished([FromQuery] int moderatorUserId)
        {
            if (!await IsModeratorAsync(moderatorUserId).ConfigureAwait(false))
            {
                return Forbid();
            }

            var listings = await _context.Listings
                .Where(l => !l.IsPublished || l.IsModerationBlocked)
                .Include(l => l.Artist)
                    .ThenInclude(a => a.ProfilePic)
                .Include(l => l.ArtCategory)
                .Include(l => l.CoverPic)
                .OrderByDescending(l => l.Created)
                .ToListAsync()
                .ConfigureAwait(false);

            await HydrateListingArtistsAsync(listings).ConfigureAwait(false);

            return Ok(listings.Select(MapListingForApi));
        }

        // Mirror frontend convention: byID route to fetch listing by numeric ID
        [HttpGet("byID/{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<Listing>> GetListingByID(int id)
        {
            if (_context.Listings == null)
            {
                return NotFound();
            }
            var listing = await _context.Listings
                .Include(l => l.Artist)
                    .ThenInclude(a => a.ProfilePic)
                .Include(l => l.ArtCategory)
                .Include(l => l.CoverPic)
                .Include(l => l.Gallery!)
                    .ThenInclude(g => g.GalleryItems)
                    .ThenInclude(gi => gi.Picture)
                .Include(l => l.Gallery!)
                    .ThenInclude(g => g.GalleryItems)
                    .ThenInclude(gi => gi.Video)
                .FirstOrDefaultAsync(l => l.ListingID == id);

            if (listing == null)
            {
                return NotFound();
            }

            await HydrateListingArtistAsync(listing).ConfigureAwait(false);

            return Ok(MapListingForApi(listing));
        }

        [HttpGet("artist/{artistPath}/listing/{listingPath}")]
        [AllowAnonymous]
        public async Task<ActionResult<Listing>> GetListingByArtistAndPath(string artistPath, string listingPath)
        {
            if (_context.Listings == null)
            {
                return NotFound();
            }

            var normalizedArtistPath = (artistPath ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedListingPath = (listingPath ?? string.Empty).Trim().ToLowerInvariant();
            
            var listing = await _context.Listings
                .Include(l => l.Artist)
                    .ThenInclude(a => a.ProfilePic)
                .Include(l => l.ArtCategory)
                .Include(l => l.CoverPic)
                .Include(l => l.Gallery!)
                    .ThenInclude(g => g.GalleryItems)
                    .ThenInclude(gi => gi.Picture)
                .Include(l => l.Gallery!)
                    .ThenInclude(g => g.GalleryItems)
                    .ThenInclude(gi => gi.Video)
                .FirstOrDefaultAsync(l =>
                    l.Artist.Path.ToLower() == normalizedArtistPath &&
                        l.Path.ToLower() == normalizedListingPath &&
                        l.IsPublished && !l.IsModerationBlocked &&
                        l.Artist.IsPublished && !l.Artist.IsModerationBlocked);

            if (listing == null)
            {
                return NotFound("Listing not found for the specified artist and path.");
            }

            await HydrateListingArtistAsync(listing).ConfigureAwait(false);

            return Ok(MapListingForApi(listing));
        }

        [HttpGet("artist/{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<Listing>>> GetListingByArtist(int id)
        {
            if (_context.Listings == null)
            {
                return NotFound();
            }

            // 2. Use .Where() and .ToListAsync()
            var listings = await _context.Listings
                .Include(l => l.Artist)
                    .ThenInclude(a => a.ProfilePic)
                .Include(l => l.ArtCategory)
                .Include(l => l.CoverPic)
                .Where(l => l.Artist.ArtistID == id && l.IsPublished && !l.IsModerationBlocked && l.Artist.IsPublished && !l.Artist.IsModerationBlocked)
                .ToListAsync();

            if (listings == null || !listings.Any())
            {
                listings = await _context.Listings
                    .Include(l => l.Artist)
                        .ThenInclude(a => a.ProfilePic)
                    .Include(l => l.ArtCategory)
                    .Include(l => l.CoverPic)
                    .Where(l => l.Artist.ArtistID == id && !l.IsModerationBlocked && !l.Artist.IsModerationBlocked)
                    .ToListAsync();
            }

            // 3. Check if the list is empty
            if (listings == null || !listings.Any())
            {
                return NotFound("No listings found for the specified artist.");
            }

            await HydrateListingArtistsAsync(listings).ConfigureAwait(false);

            return Ok(listings.Select(MapListingForApi));
        }

        [HttpPut("byID/{id}")]
        public async Task<IActionResult> PutListing(int id)
        {
            if (_context.Listings == null)
            {
                return NotFound();
            }

            var existing = await _context.Listings.FindAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            // Read raw body to avoid model binding and nested object validation
            using var reader = new System.IO.StreamReader(Request.Body);
            var bodyText = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(bodyText))
            {
                return BadRequest("Missing listing payload.");
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(bodyText);
            }
            catch (JsonException)
            {
                return BadRequest("Invalid JSON payload.");
            }

            var root = doc.RootElement;
            var props = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in root.EnumerateObject()) props[prop.Name] = prop.Value;

            static string GetString(JsonElement el) => el.ValueKind == JsonValueKind.Null ? null : el.GetString();

            // Map allowed properties
            if (props.TryGetValue("Title", out var p)) existing.Title = GetString(p) ?? existing.Title;
            if (props.TryGetValue("Description", out p)) existing.Description = GetString(p) ?? existing.Description;
            if (props.TryGetValue("Price", out p) && p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var price)) existing.Price = price;
            if (props.TryGetValue("Path", out p))
            {
                var newPath = GetString(p);
                if (!string.IsNullOrWhiteSpace(newPath) && newPath != existing.Path)
                {
                    if (!ValidPathRegex.IsMatch(newPath))
                    {
                        return BadRequest("Invalid path format.");
                    }
                    if (!props.TryGetValue("ArtistID", out var ap) || !ap.TryGetInt32(out var artistId))
                    {
                        artistId = existing.ArtistID;
                    }
                    if (!await IsPathUniqueForArtistAsync(newPath, artistId, id))
                    {
                        return BadRequest("Path is not unique for this artist.");
                    }
                    existing.Path = newPath;
                }
            }
            if (props.TryGetValue("ArtCategoryID", out p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var catId)) existing.ArtCategoryID = catId;
            if (props.TryGetValue("CoverPicID", out p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var pid)) existing.CoverPicID = pid;

            try
            {
                await _context.SaveChangesAsync();

                await TryWriteAuditLogAsync(
                    shortText: "Listing updated",
                    tags: "scope=audit;entity=listing;event=record;operation=update;result=success;channel=db",
                    listingId: existing.ListingID,
                    artistId: existing.ArtistID,
                    loggedData: $"listingId={existing.ListingID};artistId={existing.ArtistID};path={existing.Path};title={existing.Title}")
                    .ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ListingExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        [HttpPost]
        public async Task<ActionResult<Listing>> PostListing(Listing listing)
        {
            if (_context.Listings == null)
            {
                return Problem("Entity set 'TAGDBContext.Listings' is null.");
            }

            // Generate a unique path if not provided
            if (string.IsNullOrWhiteSpace(listing.Path))
            {
                listing.Path = await GenerateUniquePathForArtistAsync(listing.Title, listing.ArtistID);
            }

            // Ensure the path is valid and unique for this artist
            if (!ValidPathRegex.IsMatch(listing.Path))
            {
                return BadRequest("Invalid path format.");
            }

            if (!await IsPathUniqueForArtistAsync(listing.Path, listing.ArtistID))
            {
                return BadRequest("Path is not unique for this artist.");
            }

            _context.Listings.Add(listing);
            await _context.SaveChangesAsync();

            await TryWriteAuditLogAsync(
                shortText: "Listing created",
                tags: "scope=audit;entity=listing;event=record;operation=create;result=success;channel=db",
                listingId: listing.ListingID,
                artistId: listing.ArtistID,
                loggedData: $"listingId={listing.ListingID};artistId={listing.ArtistID};path={listing.Path};title={listing.Title}")
                .ConfigureAwait(false);

            return CreatedAtAction("GetListing", new { id = listing.ListingID }, listing);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteListing(int id)
        {
            if (_context.Listings == null)
            {
                return NotFound();
            }
            var listing = await _context.Listings.FindAsync(id);
            if (listing == null)
            {
                return NotFound();
            }

            _context.Listings.Remove(listing);
            await _context.SaveChangesAsync();

            await TryWriteAuditLogAsync(
                shortText: "Listing deleted",
                tags: "scope=audit;entity=listing;event=record;operation=delete;result=success;channel=db",
                critical: true,
                listingId: id,
                artistId: listing.ArtistID,
                loggedData: $"listingId={id};artistId={listing.ArtistID};path={listing.Path};title={listing.Title}")
                .ConfigureAwait(false);

            return NoContent();
        }

        private async Task TryWriteAuditLogAsync(
            string shortText,
            string tags,
            bool critical = false,
            string? longText = null,
            string? loggedData = null,
            int? userId = null,
            int? artistId = null,
            int? listingId = null)
        {
            try
            {
                _context.Set<Log>().Add(new Log
                {
                    ShortText = shortText,
                    Tags = tags,
                    Critical = critical,
                    LongText = longText,
                    LoggedData = loggedData,
                    UserID = userId,
                    ArtistID = artistId,
                    ListingID = listingId,
                    LogTimestamp = DateTime.UtcNow,
                });

                await _context.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write listing audit log. Tags: {Tags}", tags);
            }
        }

        private bool ListingExists(int id)
        {
            return (_context.Listings?.Any(e => e.ListingID == id)).GetValueOrDefault();
        }

        /// <summary>
        /// Generates a unique path for a listing based on its title within an artist's domain
        /// </summary>
        /// <param name="title">The listing title to convert into a path</param>
        /// <param name="artistId">The artist ID to scope the uniqueness check</param>
        /// <returns>A unique SEO-friendly path within the artist's domain</returns>
        private async Task<string> GenerateUniquePathForArtistAsync(string title, int artistId)
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
                path = $"listing-{Guid.NewGuid().ToString().Substring(0, 8)}";
            }

            // Make sure it's unique for this artist
            var counter = 0;
            var originalPath = path;
            
            while (await _context.Listings.AnyAsync(l => l.Path == path && l.ArtistID == artistId))
            {
                counter++;
                path = $"{originalPath}-{counter}";
            }

            _logger.LogInformation("Generated unique path {Path} for artist {ArtistId} and listing title {Title}", path, artistId, title);
            return path;
        }

        /// <summary>
        /// Validates that a path is unique among listings for a specific artist
        /// </summary>
        /// <param name="path">The path to validate</param>
        /// <param name="artistId">The artist ID to scope the uniqueness check</param>
        /// <param name="listingId">Optional listing ID to exclude from uniqueness check</param>
        /// <returns>True if the path is unique for the artist, false otherwise</returns>
        private async Task<bool> IsPathUniqueForArtistAsync(string path, int artistId, int? listingId = null)
        {
            var query = _context.Listings
                .Where(l => l.ArtistID == artistId && l.Path == path);
            
            if (listingId.HasValue)
            {
                query = query.Where(l => l.ListingID != listingId.Value);
            }
            
            return !await query.AnyAsync();
        }

        // Deprecated but kept for backward compatibility
        private async Task<string> GenerateUniquePathAsync(string title)
        {
            _logger.LogWarning("Deprecated GenerateUniquePathAsync method called. Use GenerateUniquePathForArtistAsync instead.");
            return await GenerateUniquePathForArtistAsync(title, 1); // Default to artist 1
        }

        // Deprecated but kept for backward compatibility
        private async Task<bool> IsPathUniqueAsync(string path, int? listingId = null)
        {
            _logger.LogWarning("Deprecated IsPathUniqueAsync method called. Use IsPathUniqueForArtistAsync instead.");
            
            var query = _context.Listings.AsQueryable();
            
            if (listingId.HasValue)
            {
                query = query.Where(l => l.ListingID != listingId.Value);
            }
            
            return !await query.AnyAsync(l => l.Path == path);
        }

        private static string NormalizeResourceUrl(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        private async Task<bool> IsModeratorAsync(int userId)
        {
            if (userId <= 0)
            {
                return false;
            }

            return await _context.Users
                .Where(u => u.UserID == userId)
                .Select(u => u.Moderator)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }

        private async Task HydrateListingArtistsAsync(IEnumerable<Listing> listings)
        {
            var listingList = listings?.ToList() ?? new List<Listing>();
            if (listingList.Count == 0)
            {
                return;
            }

            var missingArtistIds = listingList
                .Where(l => l.Artist == null && l.ArtistID > 0)
                .Select(l => l.ArtistID)
                .Distinct()
                .ToList();

            if (missingArtistIds.Count == 0)
            {
                return;
            }

            var artistsById = await _context.Artists
                .Where(a => missingArtistIds.Contains(a.ArtistID))
                .Include(a => a.ProfilePic)
                .ToDictionaryAsync(a => a.ArtistID)
                .ConfigureAwait(false);

            foreach (var listing in listingList)
            {
                if (listing.Artist == null && artistsById.TryGetValue(listing.ArtistID, out var artist))
                {
                    listing.Artist = artist;
                }
            }
        }

        private async Task HydrateListingArtistAsync(Listing listing)
        {
            if (listing == null || listing.Artist != null || listing.ArtistID <= 0)
            {
                return;
            }

            listing.Artist = await _context.Artists
                .Include(a => a.ProfilePic)
                .FirstOrDefaultAsync(a => a.ArtistID == listing.ArtistID)
                .ConfigureAwait(false);
        }

        private async Task<Gallery> EnsureListingGalleryAsync(Listing listing)
        {
            if (listing.GalleryID.HasValue)
            {
                var existingGallery = await _context.Galleries
                    .FirstOrDefaultAsync(g => g.GalleryID == listing.GalleryID.Value)
                    .ConfigureAwait(false);

                if (existingGallery != null)
                {
                    return existingGallery;
                }
            }

            var now = DateTime.UtcNow;
            var gallery = new Gallery
            {
                ScopeType = "listing",
                ScopeEntityID = listing.ListingID,
                OwnerArtistID = listing.ArtistID,
                IsPrimary = true,
                Title = $"Listing {listing.ListingID} Gallery",
                Created = now,
                Updated = now,
            };

            _context.Galleries.Add(gallery);
            await _context.SaveChangesAsync().ConfigureAwait(false);

            listing.GalleryID = gallery.GalleryID;
            await _context.SaveChangesAsync().ConfigureAwait(false);

            return gallery;
        }

        [HttpPost("{id}/gallery/video")]
        public async Task<ActionResult<GalleryItem>> PostListingGalleryVideo(int id, [FromBody] BlogVideoUpsertRequest request)
        {
            var listing = await _context.Listings
                .FirstOrDefaultAsync(l => l.ListingID == id)
                .ConfigureAwait(false);

            if (listing == null)
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
                .FirstOrDefaultAsync(v =>
                    v.NormalizedEmbedURL == normalizedEmbed ||
                    v.EmbedURL == request!.EmbedURL ||
                    (!string.IsNullOrWhiteSpace(normalizedSourceUrl) && v.URL != null && v.URL.ToLower() == normalizedSourceUrl))
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

            var gallery = await EnsureListingGalleryAsync(listing).ConfigureAwait(false);

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
                    Created = now,
                };

                _context.GalleryItems.Add(existingGalleryItem);
                gallery.Updated = now;
                await _context.SaveChangesAsync().ConfigureAwait(false);
            }

            var reloaded = await _context.GalleryItems
                .Include(gi => gi.Video)
                .FirstAsync(gi => gi.GalleryItemID == existingGalleryItem.GalleryItemID)
                .ConfigureAwait(false);

            return Ok(reloaded);
        }

        [HttpPut("{id}/gallery/order")]
        public async Task<ActionResult<IEnumerable<GalleryItem>>> PutListingGalleryOrder(int id, [FromBody] BlogGalleryOrderRequest request)
        {
            var listing = await _context.Listings
                .FirstOrDefaultAsync(l => l.ListingID == id)
                .ConfigureAwait(false);

            if (listing == null)
            {
                return NotFound();
            }

            var orderedItems = request?.Items ?? new List<BlogGalleryOrderItemDto>();
            var gallery = await EnsureListingGalleryAsync(listing).ConfigureAwait(false);
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
                            .FirstOrDefaultAsync(v => v.VideoID == requestedItem.VideoID.Value)
                            .ConfigureAwait(false);
                    }

                    if (resolvedVideo == null)
                    {
                        var normalizedVideoUrl = NormalizeResourceUrl(requestedItem.Url);
                        var normalizedEmbedUrl = NormalizeResourceUrl(requestedItem.EmbedURL);

                        resolvedVideo = await _context.Videos
                            .FirstOrDefaultAsync(v =>
                                (!string.IsNullOrWhiteSpace(normalizedEmbedUrl) && v.NormalizedEmbedURL == normalizedEmbedUrl) ||
                                (!string.IsNullOrWhiteSpace(normalizedVideoUrl) && v.URL != null && v.URL.ToLower() == normalizedVideoUrl))
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
                        .FirstOrDefaultAsync(p => p.PictureID == requestedItem.PictureID.Value)
                        .ConfigureAwait(false);
                }

                if (resolvedPicture == null)
                {
                    var normalizedPictureUrl = NormalizeResourceUrl(requestedItem.Url);
                    resolvedPicture = await _context.Pictures
                        .FirstOrDefaultAsync(p =>
                            p.NormalizedURL == normalizedPictureUrl ||
                            p.URL == requestedItem.Url)
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
                Created = now,
            }).ToList();

            _context.GalleryItems.AddRange(replacementItems);
            gallery.Updated = now;
            await _context.SaveChangesAsync().ConfigureAwait(false);

            var refreshedItems = await _context.GalleryItems
                .Where(gi => gi.GalleryID == gallery.GalleryID)
                .Include(gi => gi.Picture)
                .Include(gi => gi.Video)
                .OrderBy(gi => gi.SortOrder)
                .ToListAsync()
                .ConfigureAwait(false);

            return Ok(refreshedItems);
        }

        private static object MapListingForApi(Listing listing)
        {
            return new
            {
                listing.ListingID,
                Title = CoalescePlaintext(listing.Title_Plaintext, listing.Title),
                TitleRichtext = listing.Title,
                Description = CoalescePlaintext(listing.Description_Plaintext, listing.Description),
                DescriptionRichtext = listing.Description,
                listing.CatalogueID,
                listing.CommissionInquiryWelcome,
                listing.Created,
                listing.Credits,
                listing.Culture,
                listing.Date,
                listing.Department,
                listing.Locale,
                listing.Locus,
                listing.Medium,
                listing.Period,
                listing.Price,
                listing.Repository,
                listing.Rights,
                listing.TaxJurisdiction,
                listing.Work_BeginDate,
                listing.Work_CompletionDate,
                listing.IsPublished,
                listing.IsModerationBlocked,
                listing.Path,
                listing.ArtCategoryID,
                listing.ArtCategory,
                listing.ArtistID,
                Artist = listing.Artist == null
                    ? null
                    : new
                    {
                        listing.Artist.ArtistID,
                        Title = CoalescePlaintext(listing.Artist.Title_Plaintext, listing.Artist.Title),
                        TitleRichtext = listing.Artist.Title,
                        Byline = CoalescePlaintext(listing.Artist.Byline_Plaintext, listing.Artist.Byline),
                        BylineRichtext = listing.Artist.Byline,
                        listing.Artist.Path,
                        listing.Artist.ProfilePicID,
                        listing.Artist.ProfilePic,
                    },
                listing.GalleryID,
                listing.CoverPicID,
                listing.CoverPic,
                listing.Gallery,
                listing.ListingSalesItems,
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
    }
}
