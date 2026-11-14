# Regions API

Regions are the standard, staff-managed list of regional community groupings that venues and artists share (story #327, part of #267).
A region is statewide, covers several states, or is an area within one state (such as a metro area). A venue or artist can belong to several regions.
City, state and zip stay on the address.

Run `sql_scripts/327_regions.sql` before deploying. It creates the tables, adds a constraint on the region type, and seeds a starter list:
the 50 states and the District of Columbia as statewide regions, plus **Atlanta Metro** (within Georgia) as an example of an area within a state.
It is safe to re-run and never overwrites a region that staff renamed, edited or deactivated.

The old free-text `Addresses.Region` column is **not** changed. It stays until the artist migration story (#328) is complete.

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

Staff means a user flagged as a moderator (`Users.Moderator`), taken from the token. Anonymous callers get `401`, signed-in callers who may not do it get `403`.

### Rules

- Names are trimmed, at most 100 characters, and can't contain `<`, `>` or control characters. They must contain a letter or digit.
- Names and slugs are unique. Names are compared ignoring case. A duplicate returns `409`.
- Regions are never deleted. A deactivated region stays on the venues and artists that already use it, but can't be newly added.
- Setting a venue's regions: unknown ids return `400`. A venue that already has a deactivated region can keep it.
- Every staff change and every change to a venue's regions writes an audit `Log` row (`entity=region`, or `entity=venue;event=region`).

## Artists

`Linker_ArtistToRegion` exists now so venues and artists use the same region records. Choosing regions on the artist screens, the public artist profile, and the migration of the old
free-text region values are part of the artists story (#328).

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
