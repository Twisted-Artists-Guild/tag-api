// <copyright file="PictureController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using System;
    using TAGWEBAPI.Data;
    using TAGWEBAPI.Models;

    [Route("api/[controller]")]
    [ApiController]
    public class PictureController : ControllerBase
    {
        private readonly TAGDBContext context;

        public PictureController(TAGDBContext context)
        {
            this.context = context;
        }

        // GET: api/Picture
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Picture>>> GetPictures()
        {
            return await this.context.Set<Picture>().ToListAsync().ConfigureAwait(false);
        }

        // GET: api/Picture/credits?pictureIds=1,2,3
        [HttpGet("credits")]
        public async Task<ActionResult<IEnumerable<PictureCreditsDto>>> GetPictureCredits([FromQuery] string pictureIds)
        {
            var parsedIds = string.IsNullOrWhiteSpace(pictureIds)
                ? new List<int>()
                : pictureIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(id => int.TryParse(id, out var parsed) ? parsed : (int?)null)
                    .Where(id => id.HasValue && id.Value > 0)
                    .Select(id => id!.Value)
                    .Distinct()
                    .ToList();

            if (parsedIds.Count == 0)
            {
                return Ok(Array.Empty<PictureCreditsDto>());
            }

            var rows = await (
                from credit in this.context.MediaCredits
                join role in this.context.CreditRoles on credit.CreditRoleID equals role.CreditRoleID
                join party in this.context.CreditParties on credit.CreditPartyID equals party.CreditPartyID
                where credit.PictureID.HasValue && parsedIds.Contains(credit.PictureID.Value)
                select new
                {
                    PictureID = credit.PictureID!.Value,
                    SortOrder = credit.SortOrder,
                    RoleLabel = role.Label,
                    RoleKey = role.KeyName,
                    DisplayName = party.DisplayName,
                    ExternalURL = party.ExternalURL,
                    BioNote = party.BioNote,
                    CreditText = credit.CreditText,
                })
                .OrderBy(row => row.PictureID)
                .ThenBy(row => row.SortOrder)
                .ToListAsync()
                .ConfigureAwait(false);

            var payload = rows
                .GroupBy(row => row.PictureID)
                .Select(group => new PictureCreditsDto
                {
                    PictureID = group.Key,
                    Credits = group.Select(row => new PictureCreditEntryDto
                    {
                        Role = row.RoleLabel,
                        Name = row.DisplayName,
                        Url = row.ExternalURL,
                        Note = string.IsNullOrWhiteSpace(row.CreditText) ? row.BioNote : row.CreditText,
                        RoleKey = row.RoleKey,
                    }).ToList(),
                })
                .ToList();

            return Ok(payload);
        }

        // GET: api/Picture/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Picture>> GetPicture(int id)
        {
            var picture = await this.context.Set<Picture>().FindAsync(id).ConfigureAwait(false);

            if (picture == null)
            {
                return this.NotFound();
            }

            return picture;
        }


        // GET: api/Picture/by-url?url=https://...
        [HttpGet("by-url")]
        public async Task<ActionResult<Picture>> GetPictureByUrl([FromQuery] string url)
        {
            var normalizedUrl = NormalizeUrl(url);
            if (string.IsNullOrWhiteSpace(normalizedUrl))
            {
                return this.BadRequest(new { message = "url is required" });
            }

            var picture = await this.context.Set<Picture>()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => (p.NormalizedURL ?? p.URL ?? string.Empty).ToLower() == normalizedUrl)
                .ConfigureAwait(false);

            if (picture == null)
            {
                return this.NotFound();
            }

            return picture;
        }

        [HttpGet("{id}/credits")]
        public async Task<ActionResult<IEnumerable<MediaCreditDto>>> GetPictureCreditRows(int id)
        {
            if (!this.PictureExists(id))
            {
                return this.NotFound();
            }

            var credits = await this.QueryCreditsByPictureId(id).ConfigureAwait(false);
            return this.Ok(credits);
        }

        [HttpPut("{id}/credits")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<MediaCreditDto>>> PutPictureCredits(int id, [FromBody] MediaCreditsUpsertRequest request)
        {
            if (!this.PictureExists(id))
            {
                return this.NotFound();
            }

            var submittedCredits = request?.Credits ?? new List<MediaCreditInputDto>();
            var validationError = this.ValidateSubmittedCredits(submittedCredits);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return this.BadRequest(validationError);
            }

            var existingCredits = await this.context.MediaCredits
                .Where(credit => credit.PictureID == id)
                .ToListAsync()
                .ConfigureAwait(false);

            this.context.MediaCredits.RemoveRange(existingCredits);

            foreach (var submitted in submittedCredits.OrderBy(credit => credit.SortOrder))
            {
                var creditPartyId = await this.ResolveCreditPartyId(submitted).ConfigureAwait(false);
                if (!creditPartyId.HasValue)
                {
                    return this.BadRequest("Unable to resolve CreditPartyID for one or more credits.");
                }

                this.context.MediaCredits.Add(new MediaCredit
                {
                    PictureID = id,
                    CreditRoleID = submitted.CreditRoleID,
                    CreditPartyID = creditPartyId.Value,
                    CreditText = submitted.CreditText,
                    SortOrder = submitted.SortOrder,
                });
            }

            await this.context.SaveChangesAsync().ConfigureAwait(false);
            return this.Ok(await this.QueryCreditsByPictureId(id).ConfigureAwait(false));
        }

        [HttpGet("video/{id}/credits")]
        public async Task<ActionResult<IEnumerable<MediaCreditDto>>> GetVideoCreditRows(int id)
        {
            if (!this.context.Set<Video>().Any(video => video.VideoID == id))
            {
                return this.NotFound();
            }

            var credits = await this.QueryCreditsByVideoId(id).ConfigureAwait(false);
            return this.Ok(credits);
        }

        [HttpPut("video/{id}/credits")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<MediaCreditDto>>> PutVideoCredits(int id, [FromBody] MediaCreditsUpsertRequest request)
        {
            if (!this.context.Set<Video>().Any(video => video.VideoID == id))
            {
                return this.NotFound();
            }

            var submittedCredits = request?.Credits ?? new List<MediaCreditInputDto>();
            var validationError = this.ValidateSubmittedCredits(submittedCredits);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return this.BadRequest(validationError);
            }

            var existingCredits = await this.context.MediaCredits
                .Where(credit => credit.VideoID == id)
                .ToListAsync()
                .ConfigureAwait(false);

            this.context.MediaCredits.RemoveRange(existingCredits);

            foreach (var submitted in submittedCredits.OrderBy(credit => credit.SortOrder))
            {
                var creditPartyId = await this.ResolveCreditPartyId(submitted).ConfigureAwait(false);
                if (!creditPartyId.HasValue)
                {
                    return this.BadRequest("Unable to resolve CreditPartyID for one or more credits.");
                }

                this.context.MediaCredits.Add(new MediaCredit
                {
                    VideoID = id,
                    CreditRoleID = submitted.CreditRoleID,
                    CreditPartyID = creditPartyId.Value,
                    CreditText = submitted.CreditText,
                    SortOrder = submitted.SortOrder,
                });
            }

                await this.context.SaveChangesAsync().ConfigureAwait(false);
                return this.Ok(await this.QueryCreditsByVideoId(id).ConfigureAwait(false));
        }

        // POST: api/Picture
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<Picture>> PostPicture(Picture picture)
        {
            var normalizedUrl = NormalizeUrl(picture.NormalizedURL ?? picture.URL);
            if (!string.IsNullOrWhiteSpace(normalizedUrl))
            {
                picture.NormalizedURL = normalizedUrl;

                var existing = await this.context.Set<Picture>()
                    .FirstOrDefaultAsync(p => (p.NormalizedURL ?? p.URL ?? string.Empty).ToLower() == normalizedUrl)
                    .ConfigureAwait(false);

                if (existing != null)
                {
                    existing.AltText = picture.AltText ?? existing.AltText;
                    existing.ArtistID = picture.ArtistID ?? existing.ArtistID;
                    existing.Context = picture.Context ?? existing.Context;
                    existing.Description = picture.Description ?? existing.Description;
                    existing.EmbedURL = picture.EmbedURL ?? existing.EmbedURL;
                    existing.Height = picture.Height ?? existing.Height;
                    existing.Path = picture.Path ?? existing.Path;
                    existing.ThumbnailHeight = picture.ThumbnailHeight ?? existing.ThumbnailHeight;
                    existing.ThumbnailURL = picture.ThumbnailURL ?? existing.ThumbnailURL;
                    existing.ThumbnailWidth = picture.ThumbnailWidth ?? existing.ThumbnailWidth;
                    existing.Title = picture.Title ?? existing.Title;
                    existing.URL = picture.URL ?? existing.URL;
                    existing.Byline = picture.Byline ?? existing.Byline;
                    existing.UserID = picture.UserID ?? existing.UserID;
                    existing.Width = picture.Width ?? existing.Width;
                    existing.Updated = picture.Updated == default ? DateTime.UtcNow : picture.Updated;

                    await this.context.SaveChangesAsync().ConfigureAwait(false);
                    return this.Ok(existing);
                }
            }

            this.context.Set<Picture>().Add(picture);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.CreatedAtAction("GetPicture", new { id = picture.PictureID }, picture);
        }

        // PUT: api/Picture/5
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> PutPicture(int id, Picture picture)
        {
            if (id != picture.PictureID)
            {
                return this.BadRequest();
            }

            this.context.Entry(picture).State = EntityState.Modified;

            try
            {
                await this.context.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!this.PictureExists(id))
                {
                    return this.NotFound();
                }
                else
                {
                    throw;
                }
            }

            return this.NoContent();
        }

        // DELETE: api/Picture/5
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeletePicture(int id)
        {
            var picture = await this.context.Set<Picture>().FindAsync(id).ConfigureAwait(false);
            if (picture == null)
            {
                return this.NotFound();
            }

            this.context.Set<Picture>().Remove(picture);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.NoContent();
        }

        private bool PictureExists(int id)
        {
            return this.context.Set<Picture>().Any(e => e.PictureID == id);
        }

        private static string NormalizeUrl(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        private async Task<List<MediaCreditDto>> QueryCreditsByPictureId(int pictureId)
        {
            return await (
                from credit in this.context.MediaCredits
                join role in this.context.CreditRoles on credit.CreditRoleID equals role.CreditRoleID
                join party in this.context.CreditParties on credit.CreditPartyID equals party.CreditPartyID
                where credit.PictureID == pictureId
                orderby credit.SortOrder, role.DisplayOrder, role.Label
                select new MediaCreditDto
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
        }

        private async Task<List<MediaCreditDto>> QueryCreditsByVideoId(int videoId)
        {
            return await (
                from credit in this.context.MediaCredits
                join role in this.context.CreditRoles on credit.CreditRoleID equals role.CreditRoleID
                join party in this.context.CreditParties on credit.CreditPartyID equals party.CreditPartyID
                where credit.VideoID == videoId
                orderby credit.SortOrder, role.DisplayOrder, role.Label
                select new MediaCreditDto
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
        }

        private string ValidateSubmittedCredits(List<MediaCreditInputDto> submittedCredits)
        {
            foreach (var submitted in submittedCredits)
            {
                if (submitted.CreditRoleID <= 0)
                {
                    return "Each credit must include a valid CreditRoleID.";
                }

                if (submitted.CreditPartyID.HasValue)
                {
                    continue;
                }

                if (submitted.Party == null)
                {
                    return "Each credit must include either CreditPartyID or Party details.";
                }

                var hasLinkedIdentity = submitted.Party.UserID.HasValue || submitted.Party.ArtistID.HasValue;
                var hasDisplayName = !string.IsNullOrWhiteSpace(submitted.Party.DisplayName);
                if (!hasLinkedIdentity && !hasDisplayName)
                {
                    return "Each Party requires a linked identity or a display name.";
                }
            }

            return string.Empty;
        }

        private async Task<int?> ResolveCreditPartyId(MediaCreditInputDto submitted)
        {
            if (submitted.CreditPartyID.HasValue)
            {
                return submitted.CreditPartyID;
            }

            if (submitted.Party == null)
            {
                return null;
            }

            var party = new CreditParty
            {
                UserID = submitted.Party.UserID,
                ArtistID = submitted.Party.ArtistID,
                DisplayName = submitted.Party.DisplayName,
                ExternalURL = submitted.Party.ExternalURL,
                BioNote = submitted.Party.BioNote,
                Created = DateTime.UtcNow,
            };

            this.context.CreditParties.Add(party);
            await this.context.SaveChangesAsync().ConfigureAwait(false);
            return party.CreditPartyID;
        }

        public class PictureCreditsDto
        {
            public int PictureID { get; set; }

            public List<PictureCreditEntryDto> Credits { get; set; } = new();
        }

        public class PictureCreditEntryDto
        {
            public string? Role { get; set; }

            public string? Name { get; set; }

            public string? Url { get; set; }

            public string? Note { get; set; }

            public string? RoleKey { get; set; }
        }

        public class MediaCreditsUpsertRequest
        {
            public List<MediaCreditInputDto> Credits { get; set; } = new();
        }

        public class MediaCreditInputDto
        {
            public int? CreditPartyID { get; set; }

            public int CreditRoleID { get; set; }

            public int SortOrder { get; set; }

            public string? CreditText { get; set; }

            public MediaCreditPartyInputDto? Party { get; set; }
        }

        public class MediaCreditPartyInputDto
        {
            public int? UserID { get; set; }

            public int? ArtistID { get; set; }

            public string? DisplayName { get; set; }

            public string? ExternalURL { get; set; }

            public string? BioNote { get; set; }
        }

        public class MediaCreditDto
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
    }
}
