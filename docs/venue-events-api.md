# Venue events API

Story #332. A venue can host events, and the people who run a venue manage them from the venue portal. This page covers who can
do what, the event lifecycle, and the endpoints.

## Who manages an event

| Event | Who can change it |
| --- | --- |
| Has an artist (`artistID` set) | That artist's users, and staff |
| Hosted by the venue (`artistID` empty) | The venue's owners, and staff |

- A venue's owners can **see** an artist's published event at their venue, but cannot change it. Staff can change any event.
- Events at an unowned venue can only be hosted by staff.
- An artist's user can create an event for their own artist at any **public** venue (it tags the venue). A venue that is still a
  draft can only be tagged by its own owners.
- The user always comes from the sign-in token. A user id in a request is never trusted.

`GET /api/event/{id}` and the list endpoints return `canManage` (only set where the caller is known) so a page can show view-only
events differently.

## Lifecycle

`Draft` (private) → `Published` (public) → `Cancelled`. Only published events appear in `GET /api/event` and on the venue page.

| Action | Endpoint | From | To |
| --- | --- | --- | --- |
| Publish | `POST /api/event/{id}/publish` | Draft, PendingReview | Published |
| Unpublish | `POST /api/event/{id}/unpublish` | Published, PendingReview | Draft |
| Cancel | `POST /api/event/{id}/cancel` | Draft, Published, PendingReview | Cancelled |

- Repeating an action that already holds returns 200. Any other state returns 409. Once an event is cancelled, archived or blocked
  by moderation, only staff can change it.
- Staff can run all three on any event. That is how a disputed event is pulled.
- **Publish needs the minimum information.** If something is missing the response is 400 with `problems`, a list of plain sentences:
  a title, a description, a point of contact, an end time after the start time, doors at or before the start, an event that has not
  already ended, and a public venue.
- A published event has to stay complete: an edit that would break these rules is refused with the same `problems` list.
- Every status change, create and update writes an audit log row (`entity=event`).

## Endpoints

| Method and path | Who | Notes |
| --- | --- | --- |
| `GET /api/event?venueId=&upcoming=&limit=` | Anyone | Published events only (from #334) |
| `GET /api/event/{id}` | Anyone for public events | A draft or an event in review is only returned to its managers (404 for everyone else) |
| `GET /api/event/venue/{venueId}/manage` | The venue's owners, staff | The venue's own events in any state, other people's events once published, newest first, at most 200. Staff also see blocked events |
| `POST /api/event/venue/{venueId}` | The venue's owners, staff | Creates a venue-hosted **draft**. Needs `title`, `startTime`, `endTime`. The path is made from the title and made unique |
| `PUT /api/event/{id}/details` | Event managers | Changes only the fields that are sent |
| `POST /api/event` | Signed-in, see below | Creates an event. With no `artistID` it is venue-hosted. Non-staff events always start as drafts |
| `PUT /api/event/{id}` | Event managers | Replaces the event's fields. Only staff can change `artistID` or `statusID` |
| `DELETE /api/event/{id}` | Event managers | Owners can delete drafts only. Staff can delete any event |
| `POST /api/event/reserve-slug`, `PATCH /api/event/{id}/update-slug` | Same rules as create and edit | `artistID` is optional on reserve. A path that is taken returns 409 |

`POST /api/event` and `reserve-slug` check the same rule: an `artistID` needs that artist's user, no `artistID` needs the venue's owner.

### Event details body

```json
{
  "title": "Open Mic Night",
  "description": "<p>Rich text (HTML)</p>",
  "pointOfContact": "events@example.com",
  "startTime": "2026-12-01T19:00:00Z",
  "endTime": "2026-12-01T22:00:00Z",
  "doors": "2026-12-01T18:30:00Z",
  "cost": 10,
  "minimumAge": 18,
  "maxOccupancy": 120
}
```

- Times are stored as UTC. A time with no zone is taken as UTC, which is how the site shows event times.
- The title is stored as text-safe markup with its plain text beside it, and the description's plain text is worked out on the server.
- Limits: title 255 characters, point of contact 1000, cost 0 to 1,000,000, minimum age 0 to 120, occupancy 0 to 1,000,000.

## Database

Run `sql_scripts/332_venue_hosted_events.sql` before deploying the API. It makes `Events.ArtistID` optional and adds an index on
`(VenueID, StartTime)`. It is safe to run again and changes no data.
