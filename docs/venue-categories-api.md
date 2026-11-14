# Venue categories API

Venues are classified with categories from a staff-managed list (story #325, part of #267). A venue can have several categories.
Categories are never deleted: deactivating one keeps it on the venues that already use it, but it can't be chosen for other venues.

Run `sql_scripts/325_venue_categories.sql` before deploying. It creates the tables and seeds the starter list
(camping grounds, theaters, bars, galleries, clubs, convention centers, studios, other event spaces). It is safe to re-run and never overwrites a category that staff renamed or deactivated.

## Endpoints

| Endpoint | Who | What |
|---|---|---|
| `GET /api/VenueCategory` | anyone | Active categories in display order: `venueCategoryID`, `name`, `slug`, `isActive`. |
| `GET /api/VenueCategory/admin/all` | staff | Every category, active or not, with `sortOrder` and `venueCount`. |
| `POST /api/VenueCategory` | staff | Create. Body `{ "name": "Rooftops", "sortOrder": 90 }` (`sortOrder` optional, defaults to after the last one). `201` with the category. |
| `PUT /api/VenueCategory/{id}` | staff | Rename (and optionally reorder). The slug does **not** change, so shared filter links keep working. |
| `PATCH /api/VenueCategory/{id}/active` | staff | Body `{ "isActive": false }` deactivates, `true` reactivates. |
| `PUT /api/Venue/{venueId}/categories` | venue owner or staff | Replaces the venue's categories. Body `{ "categoryIds": [2, 5] }`. An empty list clears them. |

Staff means a user flagged as a moderator (`Users.Moderator`), taken from the token. Anonymous callers get `401`, signed-in callers who may not do it get `403`.

### Rules

- Names are trimmed, at most 100 characters, and can't contain `<`, `>` or control characters. They must contain a letter or digit.
- Names and slugs are unique. Names are compared ignoring case. A duplicate returns `409`.
- Setting a venue's categories: unknown ids return `400`. A deactivated category can't be newly added, but a venue that already has it can keep it.
- Every staff change and every change to a venue's categories writes an audit `Log` row (`entity=venueCategory` or `entity=venue;event=category`).

## In venue responses

Every venue response (list, `{id}`, `byID`, `by-slug`, `Linker_UserToVenue/byUserID`) includes the venue's categories, active or not:

```json
"categories": [
  { "venueCategoryID": 2, "name": "Theaters", "slug": "theaters", "isActive": true }
]
```

Filter by the `slug`. It stays the same when a category is renamed.
