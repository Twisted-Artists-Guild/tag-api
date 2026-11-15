# Venue profile API (stories #330 and #331)

The calls the venue onboarding wizard (`/join/venue/...`) and the portal edit page (`/portal/venue/{slug}/edit`) use to read and save a venue. All of them need a signed-in caller. The caller always comes from the token; a user id in a request is never trusted.

Who may change a venue: its owners and staff (users flagged as moderators). Permission is checked first, before the request body is read, so a stranger gets `403` whatever they send.

## Calls

| Call | Purpose |
|------|---------|
| `GET /api/venue/byID/{id}`, `GET /api/venue/by-slug/{slug}` | Load the venue with its address, categories, tags and regions. Drafts are only returned to owners and staff. |
| `GET /api/venue/{id}/access` | Whether the caller may edit this venue: `{ canManage, isStaff, isOwner }`. Used by the edit pages to turn everyone else away. `401` when signed out, `404` for an unknown venue. |
| `PUT /api/venue/{id}` | Name, description (rich text), events-page URL and the published flag. |
| `PUT /api/venue/{id}/address` | The street address (below). |
| `PUT /api/venue/{id}/categories` | `{ "categoryIds": [1, 2] }`. See `venue-categories-api.md`. |
| `PUT /api/venue/{id}/tags` | `{ "tags": ["parking", "stage"] }`. See `venue-tags-api.md`. |
| `PUT /api/venue/{id}/regions` | `{ "regionIds": [3, 7] }`. See `regions-api.md`. |

## Address

`PUT /api/venue/{id}/address`

```json
{ "addressLine1": "12 Peach St", "addressLine2": "Suite 4", "city": "Savannah", "state": "GA", "zipCode": "31401" }
```

Returns `204`. Rules:

- `addressLine1` and `city` are required (at most 255 and 100 characters). `addressLine2` is optional and at most 255 characters.
- `state` is a US state or DC, sent as the two-letter code or the full name. It is saved as the code.
- `zipCode` is 5 digits, or 5 digits, a dash and 4 digits.
- The country is saved as `USA`.
- The free-text `Region` on the address is left alone.
- The address row always belongs to this venue. If another venue still shares the row, this venue gets a new row, so saving one venue can never change another.
- Each save writes an audit log row.

## Publishing

`PUT /api/venue/{id}` with `isPublished: true` returns `400` ("Add the venue's address before publishing.") while the venue still has the placeholder address that slug reservation creates. A published venue is listed in the directory straight away, with no approval step.
Saving a venue that is already published is never blocked by this rule.

## Description

The description is stored exactly as sent, up to 20,000 characters. The website sanitizes it before saving (the same rich-text rules as the rest of the site) and again wherever it is shown. Anything else that renders it must sanitize it too.

## Public venue page (story #334)

- **`hasOwner`:** venue reads (`GET /api/venue/{id}`, `/byID/{id}`, `/by-slug/{slug}`) return `hasOwner`, true when someone manages the venue. It never says who. The public page uses it to show "Own this venue? Contact us" on unowned venues.
- **Draft and blocked venues:** they are still not public. Only their owners and staff can read them, so the page can preview them for those people.
- **Upcoming events:** `GET /api/event?venueId=5&upcoming=true&limit=10` lists a venue's published events that have not ended, soonest first. `upcoming` keeps events that are on now, `limit` defaults to 50 and is never more than 50, and either filter orders the list by start time. Without `venueId` and `upcoming` the list is the same as before.

## Moderation

Only staff can change `isModerationBlocked`. A client that sends the stored value back unchanged is accepted, so the wizard can send the whole venue.

## Pictures (story #333)

A venue has three stored pictures: `logoPicID`, `profilePicID` and `coverPicID`. Venue reads return each ID and the picture itself (`logoPic`, `profilePic`, `coverPic`). The files live in `platformpics/venue/{id}/{profile|cover|logo|gallery}/`; the gallery is just the files in its folder.

`sql_scripts/333_venue_profile_cover_pictures.sql` adds the two new columns. Run it on dev and production before deploying; until it has run, venue reads fail because the columns are missing.

| Call | Purpose |
|------|---------|
| `PUT /api/venue/{id}/pictures/profile` and `.../cover` | Make a picture the active profile or cover. Body `{ "pictureID": 12 }`; `null` clears it. Returns `{ profilePicID, coverPicID }`. |
| `POST /api/Logo/Venue/{id}` | Set the logo (existing). Venue owners can now call it; before, only staff could. |

Rules for the profile and cover call:

- Owners and staff only; anyone else gets `403` before the body is read.
- The picture must exist and be a file directly inside this venue's own folder for that kind. A picture in another venue's folder, in the wrong folder, in an archive subfolder, or somewhere else is rejected with `400`.
- Only `profile` and `cover` are valid; any other kind is `404`.
- Each change writes an audit log row.

Uploading, renaming and deleting the files themselves happen in the website's `/api/image/*` routes, which check ownership with `GET /api/venue/{id}/access`.
