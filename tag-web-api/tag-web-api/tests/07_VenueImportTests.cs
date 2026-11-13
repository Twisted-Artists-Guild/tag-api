using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TAGWEBAPI.Controllers;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;
using Xunit;
using static tagApiUnitTests.VenueTestSupport;

namespace tagApiUnitTests;

// Story #338: staff bulk-import venues. Idempotent, per-row validation, staff only, audit-logged.
public class VenueImportTests
{
    private const int StaffId = 99;
    private const int MemberId = 10;

    private static VenueImportController CreateImporter(TAGDBContext context, int? userId)
    {
        var identity = userId.HasValue
            ? new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) }, "test")
            : new ClaimsIdentity();
        return new VenueImportController(context, NullLogger<VenueImportController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } },
        };
    }

    private static async Task<DbContextOptions<TAGDBContext>> SeedAsync(string databaseName)
    {
        var options = CreateOptions(databaseName);
        await VenueTestSupport.SeedAsync(options, null, null, StaffId);
        await using var seed = new TAGDBContext(options);
        seed.Users.Add(new User { UserID = MemberId, EmailOne = "member@example.com", Username = "member" });
        await seed.SaveChangesAsync();
        return options;
    }

    private static VenueImportItem Row(string name = "Terminal West", string street = "887 W Marietta St", string city = "Atlanta", string state = "GA", string? url = null, string? region = null) => new()
    {
        Name = name,
        AddressLine1 = street,
        City = city,
        State = state,
        ZipCode = "30318",
        EventSourceUrl = url,
        RegionHint = region,
    };

    private static VenueImportRequest Batch(params VenueImportItem?[] items) => new() { Venues = items.ToList() };

    private static VenueImportResponse Ok(ActionResult<VenueImportResponse> result) =>
        Assert.IsType<VenueImportResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static async Task<VenueImportResponse> Import(DbContextOptions<TAGDBContext> options, params VenueImportItem?[] items)
    {
        await using var context = new TAGDBContext(options);
        return Ok(await CreateImporter(context, StaffId).ImportVenues(Batch(items)));
    }

    // ---- permissions

    [Fact]
    public async Task Import_Anonymous_IsUnauthorized()
    {
        var options = await SeedAsync(nameof(Import_Anonymous_IsUnauthorized));
        await using var context = new TAGDBContext(options);

        var result = await CreateImporter(context, null).ImportVenues(Batch(Row()));

        Assert.IsType<UnauthorizedResult>(result.Result);
        Assert.Empty(context.Set<Venue>());
    }

    [Fact]
    public async Task Import_NonStaff_IsForbidden()
    {
        var options = await SeedAsync(nameof(Import_NonStaff_IsForbidden));
        await using var context = new TAGDBContext(options);

        var result = await CreateImporter(context, MemberId).ImportVenues(Batch(Row()));

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Empty(context.Set<Venue>());
    }

    // ---- create

    [Fact]
    public async Task Import_CreatesPublishedUnownedVenuesWithAddress()
    {
        var options = await SeedAsync(nameof(Import_CreatesPublishedUnownedVenuesWithAddress));

        var response = await Import(options, Row(url: "https://example.com/events", region: "Midtown"), Row("The Earl", "488 Flat Shoals Ave SE", "Atlanta", "GA"));

        Assert.Equal(2, response.Created);
        Assert.All(response.Rows, r => Assert.NotNull(r.VenueID));

        await using var check = new TAGDBContext(options);
        var venues = await check.Set<Venue>().Include(v => v.Address).OrderBy(v => v.VenueID).ToListAsync();
        Assert.Equal(2, venues.Count);
        Assert.All(venues, v => Assert.True(v.IsPublished));
        Assert.All(venues, v => Assert.False(v.IsModerationBlocked));
        Assert.Empty(check.Set<Linker_UserToVenue>());
        Assert.Equal("887 W Marietta St", venues[0].Address!.AddressLine1);
        Assert.Equal("Midtown", venues[0].Address!.Region);
        Assert.Equal("https://example.com/events", venues[0].EventSourceUrl);
    }

    // ---- idempotency

    [Fact]
    public async Task Import_RerunningTheSameBatch_CreatesNoDuplicates()
    {
        var options = await SeedAsync(nameof(Import_RerunningTheSameBatch_CreatesNoDuplicates));
        var items = new[] { Row(), Row("The Earl", "488 Flat Shoals Ave SE") };

        var first = await Import(options, items);
        var second = await Import(options, items);

        Assert.Equal(2, first.Created);
        Assert.Equal(0, second.Created);
        Assert.Equal(2, second.Unchanged);
        Assert.Equal(first.Rows.Select(r => r.VenueID), second.Rows.Select(r => r.VenueID));

        await using var check = new TAGDBContext(options);
        Assert.Equal(2, await check.Set<Venue>().CountAsync());
        Assert.Equal(2, await check.Set<Address>().CountAsync(a => a.AddressID != AddressId));
    }

    [Fact]
    public async Task Import_MatchesDespiteCaseSpacingPunctuationAndStreetAbbreviations()
    {
        var options = await SeedAsync(nameof(Import_MatchesDespiteCaseSpacingPunctuationAndStreetAbbreviations));
        await Import(options, Row("Terminal West", "887 West Marietta Street"));

        var second = await Import(options, Row("  TERMINAL   west ", "887 w. marietta st.", "atlanta", "ga"));

        Assert.Equal(0, second.Created);
        Assert.Equal(VenueImportController.StatusUnchanged, second.Rows[0].Status);
        await using var check = new TAGDBContext(options);
        Assert.Single(await check.Set<Venue>().ToListAsync());
    }

    [Fact]
    public async Task Import_Rerun_UpdatesEventSourceUrlAndRegion()
    {
        var options = await SeedAsync(nameof(Import_Rerun_UpdatesEventSourceUrlAndRegion));
        await Import(options, Row());

        var second = await Import(options, Row(url: "https://example.com/calendar", region: "West Midtown"));

        Assert.Equal(1, second.Updated);
        Assert.Equal(0, second.Created);
        await using var check = new TAGDBContext(options);
        var venue = await check.Set<Venue>().Include(v => v.Address).SingleAsync();
        Assert.Equal("https://example.com/calendar", venue.EventSourceUrl);
        Assert.Equal("West Midtown", venue.Address!.Region);
    }

    [Fact]
    public async Task Import_SameVenueTwiceInOneBatch_CreatesOne()
    {
        var options = await SeedAsync(nameof(Import_SameVenueTwiceInOneBatch_CreatesOne));

        var response = await Import(options, Row(), Row("terminal west", "887 W Marietta Street"));

        Assert.Equal(1, response.Created);
        Assert.Equal(VenueImportController.StatusUnchanged, response.Rows[1].Status);
        Assert.Equal(response.Rows[0].VenueID, response.Rows[1].VenueID);
        await using var check = new TAGDBContext(options);
        Assert.Single(await check.Set<Venue>().ToListAsync());
    }

    [Fact]
    public async Task Import_SameNameAtAnotherAddress_IsSkippedBecauseThePageUrlWouldClash()
    {
        var options = await SeedAsync(nameof(Import_SameNameAtAnotherAddress_IsSkippedBecauseThePageUrlWouldClash));
        await Import(options, Row());

        var second = await Import(options, Row("Terminal West", "1 Other Rd", "Decatur", "GA"));

        Assert.Equal(1, second.Skipped);
        Assert.Contains("page URL", second.Rows[0].Message);
        await using var check = new TAGDBContext(options);
        Assert.Single(await check.Set<Venue>().ToListAsync());
    }

    [Fact]
    public async Task Import_OwnedAndBlockedVenues_AreNotModified()
    {
        var options = await SeedAsync(nameof(Import_OwnedAndBlockedVenues_AreNotModified));
        await Import(options, Row(), Row("The Earl", "488 Flat Shoals Ave SE"));
        await using (var seed = new TAGDBContext(options))
        {
            var venues = await seed.Set<Venue>().OrderBy(v => v.VenueID).ToListAsync();
            seed.Set<Linker_UserToVenue>().Add(new Linker_UserToVenue { UserID = MemberId, VenueID = venues[0].VenueID, Role = Linker_UserToVenue.OwnerRole });
            venues[1].IsModerationBlocked = true;
            await seed.SaveChangesAsync();
        }

        var second = await Import(options, Row(url: "https://example.com/new"), Row("The Earl", "488 Flat Shoals Ave SE", url: "https://example.com/earl"));

        Assert.Equal(2, second.Unchanged);
        Assert.Equal(0, second.Updated);
        await using var check = new TAGDBContext(options);
        Assert.All(await check.Set<Venue>().ToListAsync(), v => Assert.Null(v.EventSourceUrl));
    }

    // ---- validation

    [Fact]
    public async Task Import_InvalidRows_AreReportedAndTheRestIsImported()
    {
        var options = await SeedAsync(nameof(Import_InvalidRows_AreReportedAndTheRestIsImported));

        var response = await Import(
            options,
            Row("Good Venue", "1 Good St"),
            Row(name: " "),
            Row("Bad Url", "2 Bad St", url: "ftp://example.com"),
            Row("Creds", "3 Cred St", url: "https://user:pw@example.com"),
            Row("<b>Markup</b>", "4 Markup St"),
            Row("Long", new string('x', 300)),
            Row("No City", "5 St", city: ""),
            Row("!!!", "6 Symbol St"),
            null);

        Assert.Equal(9, response.Total);
        Assert.Equal(1, response.Created);
        Assert.Equal(8, response.Skipped);
        Assert.All(response.Rows.Skip(1), r => Assert.Equal(VenueImportController.StatusSkipped, r.Status));
        Assert.All(response.Rows.Skip(1), r => Assert.False(string.IsNullOrWhiteSpace(r.Message)));
        Assert.Equal(Enumerable.Range(0, 9), response.Rows.Select(r => r.Index));
        await using var check = new TAGDBContext(options);
        Assert.Single(await check.Set<Venue>().ToListAsync());
    }

    [Fact]
    public async Task Import_EmptyOrMissingBatch_IsBadRequest()
    {
        var options = await SeedAsync(nameof(Import_EmptyOrMissingBatch_IsBadRequest));
        await using var context = new TAGDBContext(options);
        var importer = CreateImporter(context, StaffId);

        Assert.IsType<BadRequestObjectResult>((await importer.ImportVenues(new VenueImportRequest { Venues = new() })).Result);
        Assert.IsType<BadRequestObjectResult>((await importer.ImportVenues(new VenueImportRequest { Venues = null })).Result);
    }

    [Fact]
    public async Task Import_BatchOverTheLimit_IsBadRequestAndImportsNothing()
    {
        var options = await SeedAsync(nameof(Import_BatchOverTheLimit_IsBadRequestAndImportsNothing));
        await using var context = new TAGDBContext(options);
        var items = Enumerable.Range(0, VenueImportController.MaxRowsPerRequest + 1).Select(i => Row($"Venue {i}", $"{i} Main St")).ToArray();

        var result = await CreateImporter(context, StaffId).ImportVenues(Batch(items));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(context.Set<Venue>());
    }

    [Fact]
    public async Task Import_BatchAtTheLimit_IsAccepted()
    {
        var options = await SeedAsync(nameof(Import_BatchAtTheLimit_IsAccepted));
        var items = Enumerable.Range(0, VenueImportController.MaxRowsPerRequest).Select(i => Row($"Venue {i}", $"{i} Main St")).ToArray();

        var response = await Import(options, items);

        Assert.Equal(VenueImportController.MaxRowsPerRequest, response.Created);
    }

    // ---- audit

    [Fact]
    public async Task Import_WritesAnAuditEntryWithTheCountsAndIds()
    {
        var options = await SeedAsync(nameof(Import_WritesAnAuditEntryWithTheCountsAndIds));

        var response = await Import(options, Row(), Row(name: ""));

        await using var check = new TAGDBContext(options);
        var log = await check.Set<Log>().SingleAsync(l => l.ShortText == "Venue import");
        Assert.Equal(StaffId, log.UserID);
        Assert.Contains("event=import", log.Tags);
        Assert.Contains("created=1;updated=0;unchanged=0;skipped=1", log.LoggedData);
        Assert.Contains($"createdVenueIds={response.Rows[0].VenueID}", log.LoggedData);
    }
}
