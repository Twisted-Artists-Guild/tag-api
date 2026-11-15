# Regions API

Regions are the standard, staff-managed list of regional community groupings that venues and artists share (story #327, part of #267).
A region is statewide, covers several states, or is an area within one state (such as a metro area). A venue or artist can belong to several regions.
City, state and zip stay on the address.

Run `sql_scripts/327_regions.sql` before deploying. It creates the tables, adds a constraint on the region type, and seeds a starter list:
the 50 states and the District of Columbia as statewide regions, plus **Atlanta Metro** (within Georgia) as an example of an area within a state.
It is safe to re-run and never overwrites a region that staff renamed, edited or deactivated.

The old free-text `Addresses.Region` column is **never changed or removed**, not even by the artist migration (below): the migration only reads it.

## Types and states

| Type | States covered |
|---|---|
| `Statewide` | exactly one state |
| `WithinState` | exactly one state (an area inside it, such as a metro area) |
| `Multistate` | two or more states |

States are two-letter codes for the 50 states and DC (`GA`, `AL`, `DC` ...). Codes are accepted in any letter case, duplicates are dropped, and they are returned upper case and sorted.
Types are accepted in any letter case and returned as `Statewide`, `Multistate` or `WithinState`.

## Endpoints

| Endpoint | Who | What |
|---|---|---|
| `GET /api/Region` | anyone | Active regions by name: `regionID`, `name`, `slug`, `type`, `states`, `isActive`. `?state=GA` returns only regions that cover that state (an unknown state returns an empty list). |
| `GET /api/Region/admin/all` | staff | Every region, active or not, with `venueCount` and `artistCount`. |
| `POST /api/Region` | staff | Create. Body `{ "name": "Gulf Coast", "type": "Multistate", "states": ["TX", "LA"] }`. `201` with the region. |
| `PUT /api/Region/{id}` | staff | Edit the name, type and states (send all three). The slug does **not** change, so shared filter links keep working. |
| `PATCH /api/Region/{id}/active` | staff | Body `{ "isActive": false }` deactivates, `true` reactivates. |
| `PUT /api/Venue/{venueId}/regions` | venue owner or staff | Replaces the venue's regions. Body `{ "regionIds": [1, 2] }`. An empty list clears them. |
| `PUT /api/Artist/{artistId}/regions` | the artist's own users or staff | Replaces the artist's regions. Same body and rules as the venue call. |
| `POST /api/Region/admin/migrate-artist-regions` | staff | Links artists to regions from their old free-text region. Previews unless `?dryRun=false` (see Artists). |

Staff means a user flagged as a moderator (`Users.Moderator`), taken from the token. Anonymous callers get `401`, signed-in callers who may not do it get `403`.

### Rules

- Names are trimmed, at most 100 characters, and can't contain `<`, `>` or control characters. They must contain a letter or digit.
- Names and slugs are unique. Names are compared ignoring case. A duplicate returns `409`.
- Regions are never deleted. A deactivated region stays on the venues and artists that already use it, but can't be newly added.
- Setting a venue's regions: unknown ids return `400`. A venue that already has a deactivated region can keep it.
- Every staff change and every change to a venue's regions writes an audit `Log` row (`entity=region`, or `entity=venue;event=region`).

## Artists

Artists use the same regions as venues, through `Linker_ArtistToRegion` (story #328). Artist responses return the artist's regions in the same shape as venues:

- `GET /api/artist/byID/{id}`, `GET /api/artist/{slug}`: `regions` at the top level of the artist.
- `GET /api/artist/{slug}/profile` (the public profile): `artist.regions`.
- The artist list (`GET /api/artist`) returns `regions: null`, meaning "not loaded", not "none".

`PUT /api/Artist/{artistId}/regions` replaces the artist's regions with `{ "regionIds": [1, 2] }`. Only users linked to the artist and staff may call it; others get `403` before the body is read. Unknown ids return `400`, a deactivated region can stay but not be newly added, and every change writes an audit row.

### Migrating the old free-text region

`POST /api/Region/admin/migrate-artist-regions` (staff only) turns the region text artists typed on their **public** addresses (`Addresses.Region`) into region links.

- **It previews by default.** Send `?dryRun=false` to apply. Run the preview first and read the report.
- **Only exact matches are made.** Text is compared ignoring case, spaces, dots and dashes. It matches a region's name or slug, or a state (code or name), which stands for that state's statewide region. Text with several values (`GA, NC`, `GA/NC`, `Georgia and North Carolina`) is split, and each part is matched on its own. "Atlanta" does not become "Atlanta Metro"; nothing is guessed.
- **Only active regions** are used, because a deactivated region can't be newly added.
- **Private addresses are never read**, so a private address can't turn into a public region.
- **An artist who already has regions is left alone.** It is safe to run again; it only adds links an artist does not have.
- **Nothing is lost.** The text stays on the address.
- It writes an audit row when it applies changes.

The report:

```json
{
  "dryRun": true,
  "artistsWithRegionText": 5,
  "artistsSkippedAlreadyHadRegions": 1,
  "artistsMigrated": 3,
  "artistsWithNoMatch": 2,
  "linksAdded": 4,
  "unmatched": [ { "text": "Narnia", "artistCount": 3, "artistIds": [2, 4, 6] } ]
}
```

`unmatched` lists the text that matched nothing, most common first, with up to 10 artist ids to look at. To handle them, either add or rename a region so the text matches, then run the migration again, or ask the artist to choose a region on their profile.

## In venue responses

Every venue response (list, `{id}`, `byID`, `by-slug`, `Linker_UserToVenue/byUserID`) includes the venue's regions, active or not:

```json
"regions": [
  { "regionID": 1, "name": "Georgia", "slug": "georgia", "type": "Statewide", "states": ["GA"], "isActive": true }
]
```

Filter by the `slug`. It stays the same when a region is renamed or edited.

## Staff import

The import's `regionHint` is still saved as text on the address. It is also matched, by name or slug and ignoring case, to an **active** region.
A match links the region to a new venue, or to an existing unowned venue that has no region yet. A region that is already set is never changed.
A hint that matches nothing is reported in the row `message`, and the venue is still imported. See [venue-import-api.md](venue-import-api.md).
