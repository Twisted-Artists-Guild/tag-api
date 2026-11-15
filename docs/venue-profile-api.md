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

## Moderation

Only staff can change `isModerationBlocked`. A client that sends the stored value back unchanged is accepted, so the wizard can send the whole venue.
