# Venue directory API

The public, paged list behind the `/venues` page (story #335, part of #267). Anyone can call it, signed in or not.

```
GET /api/venue/list
```

Only **published, non-blocked** venues are ever returned, whether or not they have an owner. Draft and blocked venues never appear, whatever the filters.

The older `GET /api/venue` still returns every published venue in one unpaged list. Use `/api/venue/list` for anything a person browses.

Run `sql_scripts/335_venue_directory_indexes.sql` before deploying (indexes only, no data changes, safe to re-run).

## Parameters

All parameters are optional, and filters combine: a venue has to match **every** filter that is given.

| Parameter | Meaning |
|---|---|
| `q` | Name search. Matches anywhere in the name, ignoring case. Uses the plain-text name, so markup is never searched. |
| `category` | A category slug (see `GET /api/VenueCategory`). Repeat it for several: a venue in **any** of them matches. |
| `region` | A region slug (see `GET /api/Region`). Repeat it for several: a venue in **any** of them matches. |
| `tag` | An amenity tag (see `GET /api/VenueTag/suggest`). Repeat it for several: a venue needs **all** of them. Tags are normalized like everywhere else (`Wheelchair  Accessible` = `wheelchair accessible`). A tag that can't exist matches nothing. |
| `city` | The whole city name, ignoring case. |
| `state` | A state code or a state name (`GA` or `Georgia`). Both match, because addresses store the state as it was typed. |
| `zip` | Matches by prefix (`303` finds `30318`). Only digits and a hyphen are used. A value with no digits is ignored. |
| `cursor` | The `nextCursor` from the previous page. Leave it out for the first page. |
| `limit` | Venues per page. Default 12, from 1 to 50. |

Text parameters are trimmed and cut at 100 characters, and each repeatable parameter uses at most 10 values.

Example: theaters or galleries in Georgia with parking and a stage, whose name contains "west":

```
GET /api/venue/list?q=west&category=theaters&category=galleries&state=GA&tag=parking&tag=stage
```

## Response

```json
{
  "items": [
    {
      "venueID": 13,
      "name": "Terminal West",
      "slug": "terminal-west",
      "description": "A short preview, at most 200 characters…",
      "city": "Atlanta",
      "state": "GA",
      "logoUrl": "https://…/logo.jpg",
      "categories": [ { "name": "Theaters", "slug": "theaters" } ],
      "tags": [ "parking", "stage" ],
      "regions": [ { "name": "Atlanta Metro", "slug": "atlanta-metro" } ]
    }
  ],
  "nextCursor": "eyJrIjoi…"
}
```

- `name` is plain text. `slug` is the page address: `/venue/{slug}`.
- `logoUrl` is `null` when the venue has no logo.
- `nextCursor` is `null` on the last page. A page with no matches returns `items: []` and no cursor.
- Nothing private is returned: no owner, no draft or moderation flags, no contact details.

## Paging

Venues come back **alphabetically by name** (ignoring case), and venues with the same name are ordered by id, so the order never changes between requests.
The cursor holds the name and id of the last venue on the page, and the next page starts right after it. This means:

- a long list is never loaded in one go,
- pages never repeat or skip a venue, even when venues are added or removed while someone is browsing,
- the filters must be sent again with the cursor (the cursor only remembers the position, not the filters).

An invalid cursor returns `400`. Treat the cursor as opaque: don't build or edit it.

## Performance

`sql_scripts/335_venue_directory_indexes.sql` adds indexes for the alphabetical order (a partial index on published, non-blocked venues), the address join, lower-case city and state, and zip prefixes.
The category, tag and region links already have indexes from their own stories.

The name search matches anywhere in the name, which an ordinary index can't serve. It is fast enough while the directory holds thousands of venues.
If it grows much larger, add a trigram index (see the comment at the top of the SQL script).

## Sample venues for testing

`sql_scripts/335_sample_venues.sql` adds 16 published sample venues (names start with `Sample `) across 9 states, with categories, amenity tags and regions, plus one unpublished and one blocked venue that the directory must not show. It can be run more than once; venues that already exist are skipped.
It needs the category, tag and region scripts (325, 326 and 327) to have run first.
`sql_scripts/335_sample_venues_remove.sql` removes them again. Use these on a shared or production database only on purpose, because the sample venues are visible in the public directory until they are removed.
