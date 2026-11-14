# Venue tags API

Venues describe their amenities with free-form tags such as "parking", "stage" or "wheelchair accessible" (story #326, part of #267).
Owners add their own tags, visitors can filter venues by tag, and staff can merge or remove tags.

Run `sql_scripts/326_venue_tags.sql` before deploying. It creates the tables and is safe to re-run. No tags are seeded.

## Normalization and rules

Every tag is normalized the same way, in the API and in the website's tag input, so "Wheelchair  Accessible" and "wheelchair accessible" are one tag:

1. Trimmed.
2. Runs of spaces (and tabs or new lines) collapse to one space.
3. Lowercased.
4. Duplicates in one request are dropped.

A tag is valid when it:

- is not empty,
- has at most **30** characters,
- starts with a letter or number and contains only letters and numbers of any language, spaces, hyphens, apostrophes and `&`.

That character set cannot contain `<`, `>`, quotes, colons or any other markup character, so a tag can never carry HTML or script.
The website shows tags as plain text only. A venue can have at most **15** tags. An invalid tag returns `400` with a message that names the problem, and nothing is changed.

## Endpoints

| Endpoint | Who | What |
|---|---|---|
| `GET /api/VenueTag/suggest?q=par&limit=10` | anyone | The most used tags that start with `q` (ignoring case), most used first. An empty `q` returns the most used tags. `limit` is 1 to 20. Tags no venue uses are not suggested. Returns `[{ "name": "parking", "venueCount": 3 }]`. |
| `PUT /api/Venue/{venueId}/tags` | venue owner or staff | Replaces the venue's tags. Body `{ "tags": ["Parking", "Stage"] }`. An empty list clears them. Returns the normalized tag names. New tags are created. |
| `GET /api/VenueTag/admin/all?search=&afterId=0&limit=50` | staff | Tags with how many venues use them, in pages. `nextAfterId` is the cursor for the next page, or `null` at the end. `limit` is 1 to 100. |
| `POST /api/VenueTag/{id}/merge` | staff | Body `{ "targetTagId": 5 }`. Moves every venue from this tag to the target, then deletes this tag. A venue that already has the target keeps one copy. |
| `DELETE /api/VenueTag/{id}` | staff | Removes the tag from every venue and deletes it. |

Staff means a user flagged as a moderator (`Users.Moderator`), taken from the token. Anonymous callers get `401`, signed-in callers who may not do it get `403`.
Every change writes an audit `Log` row (`entity=venue;event=tag` for a venue's tags, `entity=venueTag` for staff changes).

## In venue responses

Every venue response (list, `{id}`, `byID`, `by-slug`, `Linker_UserToVenue/byUserID`) includes the venue's tags as a sorted list of names:

```json
"tags": ["parking", "stage", "wheelchair accessible"]
```

## Filtering the public venue list

`GET /api/venue?tag=parking&tag=stage` returns the published venues that have **every** requested tag. Tags in the query are normalized the same way,
so `tag=Wheelchair%20Accessible` works. A tag that doesn't exist, or can't exist, matches no venue and returns an empty list.
