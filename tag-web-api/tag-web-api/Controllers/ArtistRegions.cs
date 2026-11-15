// <copyright file="ArtistRegions.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Controllers
{
    using System.Text.RegularExpressions;
    using Microsoft.EntityFrameworkCore;
    using TAGWEBAPI.Data;
    using TAGWEBAPI.Models;

    /// <summary>Who may change an artist's regions: the artist's own users, and staff (users flagged as moderators).</summary>
    internal static class ArtistAccess
    {
        public static Task<bool> IsOwnerAsync(TAGDBContext context, int artistId, int userId)
        {
            return context.Set<Linker_UserToArtist>()
                .AsNoTracking()
                .AnyAsync(l => l.ArtistID == artistId && l.UserID == userId);
        }

        public static async Task<bool> CanManageAsync(TAGDBContext context, int artistId, int userId)
        {
            return await VenueAccess.IsStaffAsync(context, userId).ConfigureAwait(false)
                || await IsOwnerAsync(context, artistId, userId).ConfigureAwait(false);
        }
    }

    /// <summary>The regions of an artist, as the artist responses return them.</summary>
    public static class ArtistRegions
    {
        public static async Task<List<RegionDto>> LoadAsync(TAGDBContext context, int artistId)
        {
            var regions = await context.Set<Linker_ArtistToRegion>()
                .AsNoTracking()
                .Where(l => l.ArtistID == artistId)
                .Select(l => l.Region!)
                .ToListAsync()
                .ConfigureAwait(false);

            return regions
                .OrderBy(r => r.Name, StringComparer.Ordinal)
                .Select(ToDto)
                .ToList();
        }

        public static RegionDto ToDto(Region region) => new()
        {
            RegionID = region.RegionID,
            Name = region.Name,
            Slug = region.Slug,
            Type = region.Type,
            States = region.States,
            IsActive = region.IsActive,
        };
    }

    /// <summary>The regions a piece of free text stands for, and the parts of it that matched nothing.</summary>
    public sealed class RegionTextMatch
    {
        public List<int> RegionIds { get; } = new();

        public List<string> Unmatched { get; } = new();
    }

    /// <summary>
    /// Turns the old free-text region an artist typed (for example "GA", "Georgia" or "Georgia, Alabama") into the shared
    /// regions. It only ever matches exactly: a region's name or slug, or a state (code or name), which stands for that
    /// state's statewide region. Anything it cannot place is handed back, never guessed. Only active regions are used.
    /// </summary>
    public static class RegionTextMapper
    {
        private static readonly Regex Separators = new(@"\s*(?:[,;/|&]|\band\b)\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string Normalize(string? value)
        {
            var text = (value ?? string.Empty).ToLowerInvariant()
                .Replace('-', ' ')
                .Replace('_', ' ')
                .Replace(".", string.Empty, StringComparison.Ordinal);
            return Regex.Replace(text, @"\s+", " ").Trim();
        }

        public static RegionTextMatch Map(string? text, IReadOnlyCollection<Region> regions)
        {
            var result = new RegionTextMatch();
            var trimmed = (text ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                return result;
            }

            var lookup = BuildLookup(regions);

            // Markup is never taken apart: it is not a region, and splitting it would report meaningless fragments.
            if (trimmed.Contains('<', StringComparison.Ordinal) || trimmed.Contains('>', StringComparison.Ordinal))
            {
                result.Unmatched.Add(trimmed);
                return result;
            }

            // The whole text first, so a region whose name contains "and" or "/" is still found.
            var whole = Resolve(trimmed, lookup);
            if (whole.HasValue)
            {
                result.RegionIds.Add(whole.Value);
                return result;
            }

            var parts = Separators.Split(trimmed).Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
            if (parts.Count <= 1)
            {
                result.Unmatched.Add(trimmed);
                return result;
            }

            foreach (var part in parts)
            {
                var id = Resolve(part, lookup);
                if (id.HasValue)
                {
                    if (!result.RegionIds.Contains(id.Value))
                    {
                        result.RegionIds.Add(id.Value);
                    }
                }
                else if (!result.Unmatched.Contains(part, StringComparer.OrdinalIgnoreCase))
                {
                    result.Unmatched.Add(part);
                }
            }

            return result;
        }

        private static Dictionary<string, int> BuildLookup(IReadOnlyCollection<Region> regions)
        {
            var lookup = new Dictionary<string, int>(StringComparer.Ordinal);
            var active = regions.Where(r => r.IsActive).OrderBy(r => r.RegionID).ToList();

            foreach (var region in active)
            {
                lookup.TryAdd(Normalize(region.Name), region.RegionID);
                lookup.TryAdd(Normalize(region.Slug), region.RegionID);
            }

            // A state, as a code or a name, stands for its statewide region. Names and slugs above win when they overlap.
            foreach (var region in active.Where(r => r.Type == RegionTypes.Statewide && r.States.Count == 1))
            {
                var code = region.States[0];
                lookup.TryAdd(Normalize(code), region.RegionID);
                if (UsStates.Names.TryGetValue(code, out var name))
                {
                    lookup.TryAdd(Normalize(name), region.RegionID);
                }
            }

            return lookup;
        }

        private static int? Resolve(string text, Dictionary<string, int> lookup)
        {
            return lookup.TryGetValue(Normalize(text), out var id) ? id : null;
        }
    }

    public sealed class UnmatchedRegionText
    {
        public string Text { get; set; } = string.Empty;

        public int ArtistCount { get; set; }

        /// <summary>The first few artists with this text, so staff can look at them.</summary>
        public List<int> ArtistIds { get; set; } = new();
    }

    public sealed class ArtistRegionMigrationReport
    {
        public bool DryRun { get; set; }

        /// <summary>Artists with a region typed on one of their public addresses.</summary>
        public int ArtistsWithRegionText { get; set; }

        /// <summary>Artists left alone because they already have regions of their own.</summary>
        public int ArtistsSkippedAlreadyHadRegions { get; set; }

        /// <summary>Artists that got at least one region (or would, on a dry run).</summary>
        public int ArtistsMigrated { get; set; }

        /// <summary>Artists whose text matched no region at all.</summary>
        public int ArtistsWithNoMatch { get; set; }

        public int LinksAdded { get; set; }

        /// <summary>Text that matched nothing, most common first. The text itself is left on the address.</summary>
        public List<UnmatchedRegionText> Unmatched { get; set; } = new();
    }

    /// <summary>
    /// Links artists to the shared regions that their old free-text region matches. The text on the address is never changed
    /// or removed. Only public addresses are read, so a private address is never turned into a public region, and an artist
    /// who already has regions is left alone. Safe to run again: it only adds links that are missing.
    /// </summary>
    public static class ArtistRegionMigration
    {
        private const int MaxArtistIdsPerText = 10;

        public static async Task<ArtistRegionMigrationReport> RunAsync(TAGDBContext context, bool apply, int actingUserId)
        {
            var regions = await context.Set<Region>().AsNoTracking().Where(r => r.IsActive).ToListAsync().ConfigureAwait(false);

            var artistIds = (await context.Set<Artist>().AsNoTracking().Select(a => a.ArtistID).ToListAsync().ConfigureAwait(false)).ToHashSet();
            var alreadyHave = (await context.Set<Linker_ArtistToRegion>().AsNoTracking().Select(l => l.ArtistID).Distinct().ToListAsync().ConfigureAwait(false)).ToHashSet();

            var rows = await context.Set<Linker_EntityToContact>()
                .AsNoTracking()
                .Where(l => l.EntityType == LinkedEntityTypes.Artist
                    && l.Scope != ContactScope.Private
                    && l.Contact.ContactType.ToLower() == "address"
                    && l.Contact.Address != null
                    && l.Contact.Address.Region != null
                    && l.Contact.Address.Region != string.Empty)
                .Select(l => new { l.EntityID, Text = l.Contact.Address!.Region! })
                .ToListAsync()
                .ConfigureAwait(false);

            var report = new ArtistRegionMigrationReport { DryRun = !apply };
            var unmatched = new Dictionary<string, UnmatchedRegionText>(StringComparer.OrdinalIgnoreCase);
            var newLinks = new List<Linker_ArtistToRegion>();

            foreach (var artist in rows.Where(r => artistIds.Contains(r.EntityID)).GroupBy(r => r.EntityID).OrderBy(g => g.Key))
            {
                report.ArtistsWithRegionText++;
                if (alreadyHave.Contains(artist.Key))
                {
                    report.ArtistsSkippedAlreadyHadRegions++;
                    continue;
                }

                var regionIds = new List<int>();
                foreach (var text in artist.Select(r => r.Text.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var match = RegionTextMapper.Map(text, regions);
                    regionIds.AddRange(match.RegionIds.Where(id => !regionIds.Contains(id)));

                    foreach (var left in match.Unmatched)
                    {
                        if (!unmatched.TryGetValue(left, out var entry))
                        {
                            entry = new UnmatchedRegionText { Text = left };
                            unmatched[left] = entry;
                        }

                        if (!entry.ArtistIds.Contains(artist.Key))
                        {
                            entry.ArtistCount++;
                            if (entry.ArtistIds.Count < MaxArtistIdsPerText)
                            {
                                entry.ArtistIds.Add(artist.Key);
                            }
                        }
                    }
                }

                if (regionIds.Count == 0)
                {
                    report.ArtistsWithNoMatch++;
                    continue;
                }

                report.ArtistsMigrated++;
                report.LinksAdded += regionIds.Count;
                newLinks.AddRange(regionIds.Select(id => new Linker_ArtistToRegion { ArtistID = artist.Key, RegionID = id }));
            }

            report.Unmatched = unmatched.Values
                .OrderByDescending(u => u.ArtistCount)
                .ThenBy(u => u.Text, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (apply && newLinks.Count > 0)
            {
                context.Set<Linker_ArtistToRegion>().AddRange(newLinks);
                context.Set<Log>().Add(new Log
                {
                    ShortText = "Artist regions migrated",
                    Tags = "scope=audit;entity=artist;event=region;operation=migrate;result=success;channel=db",
                    Critical = false,
                    LoggedData = $"actingUserId={actingUserId};artists={report.ArtistsMigrated};links={report.LinksAdded};unmatchedTexts={report.Unmatched.Count}",
                    UserID = actingUserId,
                    LogTimestamp = DateTime.UtcNow,
                });
                await context.SaveChangesAsync().ConfigureAwait(false);
            }

            return report;
        }
    }
}
