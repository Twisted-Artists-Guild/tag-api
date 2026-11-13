# Venue import API

Staff-only bulk import for seeding the venue directory, for a manual run or for the future crawler (story #338, part of #267).
It creates venues from a name and a street address, and updating works the same way: sending a venue again never creates a duplicate.

## Endpoint

```
POST /api/venue/admin/import
Authorization: Bearer <token of a staff user>
Content-Type: application/json
```

## Who can call it

Any signed-in user flagged as staff (`Users.Moderator`). There is no separate service credential or secret. A crawler
should run as its own dedicated staff user and sign in the same way the website does, so every import is attributed
to an account in the audit log. Anonymous callers get `401`, signed-in non-staff get `403`.

## Request

```json
{
  "venues": [
    {
      "name": "Terminal West",
      "addressLine1": "887 W Marietta St",
      "addressLine2": "Suite 100",
      "city": "Atlanta",
      "state": "GA",
      "zipCode": "30318",
      "country": "USA",
      "eventSourceUrl": "https://example.com/events",
      "categoryHint": "Music venue",
      "regionHint": "West Midtown"
    }
  ]
}
```

| Field | Required | Notes |
|---|---|---|
| `name` | yes | At most 1000 characters. |
| `addressLine1` | yes | Street address. At most 255 characters. |
| `city`, `state` | yes | At most 100 characters each. Use the same state format every time (`GA`, not `Georgia` on one run and `GA` on the next). |
| `addressLine2` | no | At most 255 characters. |
| `zipCode` | no | At most 20 characters. Not used for matching. |
| `country` | no | At most 100 characters. Stored as given. |
| `eventSourceUrl` | no | Absolute `http` or `https` URL, at most 2048 characters, no embedded credentials. The page a crawler can later scan for events. |
| `regionHint` | no | At most 100 characters. Stored in the address region. |
| `categoryHint` | no | At most 100 characters. Validated but **not stored yet**, because venue categories do not exist yet (story #325). |

Text must not contain `<`, `>` or control characters, so crawled markup never reaches a page.

### Limits

- At most **200 rows** per request. Send bigger imports in several requests. A larger batch is rejected with `400` and nothing is imported.
- Request body at most **512 KB**. A larger body is rejected with `413`.

## What happens to each row

- **New venue:** created **published** and **without an owner**. There is no approval step.
- **Existing venue:** a row matches an existing venue when the normalized name, street, city and state are equal.
  Case, spacing, punctuation and common street abbreviations (`Street`/`St.`, `West`/`W`) are ignored, and the zip code is not part of the match.
  A matching venue is updated with the row's `eventSourceUrl` and `regionHint` when they were sent and differ. Its name and address are never changed.
- **Never modified by an import:** venues that have an owner and venues blocked by moderation. The row is reported as `unchanged`.
  Imports also never publish, unpublish or block anything on an existing venue.
- **Same name, different address:** a venue's page URL comes from its name alone, so two venues cannot share a name.
  Such a row is `skipped` with a message. Make the name distinct, for example `Starbucks - Decatur`.
- **Same venue twice in one batch:** created once; the second row is reported as `unchanged` and points at the same `venueID`.

## Response

`200` with a summary and one entry per row, in request order. Invalid rows never fail the rest of the batch.

```json
{
  "total": 3,
  "created": 1,
  "updated": 0,
  "unchanged": 1,
  "skipped": 1,
  "rows": [
    { "index": 0, "name": "Terminal West", "status": "created", "venueID": 42, "message": null },
    { "index": 1, "name": "terminal west", "status": "unchanged", "venueID": 42, "message": "Same venue as row 0 in this batch." },
    { "index": 2, "name": "", "status": "skipped", "venueID": null, "message": "Name is required." }
  ]
}
```

| Status | Meaning |
|---|---|
| `created` | A new published, unowned venue was created. |
| `updated` | An existing venue's `eventSourceUrl` or region changed. |
| `unchanged` | The venue already exists and nothing needed to change (or it is owner-managed or blocked). |
| `skipped` | The row was invalid or clashed with another venue's name. See `message`. `index` is the zero-based position in the request. |

Other responses: `400` (no rows, or more than 200), `401`, `403`, `413`.

## Idempotency and retries

Re-sending the same batch is safe: matching rows come back as `unchanged`. If a request fails with a server error, nothing from that
batch is saved, so the whole batch can be sent again.
Two imports running at the same moment can still create the same venue twice (there is no unique database constraint), so a crawler should send batches one at a time.

## Audit

Every call writes one `Log` row (`ShortText` = `Venue import`, tags include `entity=venue;event=import`) with the acting user id,
the counts and the ids of created and updated venues.

## Out of scope

The crawler itself, scanning the stored events page, and refreshing event calendars are future work. `eventSourceUrl` is stored so they can be added later.
