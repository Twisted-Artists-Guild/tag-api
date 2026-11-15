# Venues

The venue feature (feature #267) lets anyone find a venue, lets venue owners run their venue's page and events, and lets staff seed
and moderate the directory. This page is the overview. Each part has its own detail page, linked below. The website side is in
the `tag-web` repo (`docs/venues.md`).

## What people can do

| Who | What |
| --- | --- |
| Anyone, signed out | Browse the directory with filters, open a public venue page, see a venue's upcoming events |
| Signed-in user | Register a venue (they become its owner), then see and manage the venues they own |
| Venue owner | Edit the profile, address, categories, tags, regions and pictures, publish or unpublish the venue, add and manage its contacts, and host events |
| Artist's user | Create events for their artist that tag a venue. A venue's owners can see such an event but not change it |
| Staff (`Users.Moderator`) | Everything above for any venue, plus: assign and remove owners, block a venue, import venues, manage categories, tags and regions |

Staff status and the signed-in user always come from the sign-in token. A user id in a request body or URL is never trusted.

## The flow

1. **Register.** A signed-in user reserves a name. This creates a private draft venue with the user as owner (`POST /api/venue/reserve-slug`).
2. **Describe it.** Name and rich-text description, street address, contact details, categories, amenity tags and regions.
3. **Add pictures.** Logo, profile and cover pictures, and a gallery. One picture of each kind is the active one.
4. **Publish.** A venue is only public once its owner publishes it, and it can't be published while it still has the placeholder address.
5. **It appears** in the directory (`GET /api/venue/list`) and on its public page (`/venue/<slug>`), unless staff have blocked it.
6. **Host events.** Events start as private drafts and go public when the owner publishes them ([venue-events-api.md](venue-events-api.md)).

A venue that staff import starts **published and without an owner**. An owner can later be assigned by staff. The public page then
stops asking visitors to get in touch.

## Pages on the website

| Page | Who | Purpose |
| --- | --- | --- |
| `/venues` | Anyone | Directory with search and filters |
| `/venue/<slug>` | Anyone (owners and staff can also open a draft) | Public venue page |
| `/join/venue` | Signed in | Registration wizard |
| `/portal/venue`, `/portal/venue/<slug>` | Owners, staff | Venue list and workspace, with events |
| `/portal/venue/<slug>/edit` | Owners, staff | Edit profile, media, contacts, categories, tags, regions |
| `/portal/venue/<slug>/events/new`, `.../events/<id>` | Owners, staff | Create and edit an event the venue hosts |
| `/portal/staff/venues` | Staff | Find venues, assign and remove owners |
| `/portal/staff/venue-categories`, `/portal/staff/regions` | Staff | Manage the shared lists |

## API

All routes start with `/api`. "Anonymous" means no sign-in. Everything else needs a signed-in user, and "owner or staff" is checked
against the venue in the URL. Anonymous access is pinned down by `tests/18_VenueJourneyAndAccessTests.cs`: adding an anonymous
route to any venue controller makes that test fail until the list is updated on purpose.

| Area | Endpoints | Who | Details |
| --- | --- | --- | --- |
| Directory | `GET /venue/list` | Anonymous | [venue-directory-api.md](venue-directory-api.md) |
| Venue | `GET /venue`, `GET /venue/{id}`, `GET /venue/by-slug/{slug}` | Anonymous, published and unblocked venues only | [venue-profile-api.md](venue-profile-api.md) |
| Venue | `GET /venue/byID/{id}`, `GET /venue/{id}/access` | Owner or staff see drafts | [venue-profile-api.md](venue-profile-api.md) |
| Venue | `POST /venue/reserve-slug`, `PATCH /venue/{id}/update-slug`, `POST /venue`, `PUT /venue/{id}`, `PUT /venue/{id}/address`, `PUT /venue/{id}/pictures/{kind}`, `DELETE /venue/{id}` | Signed in; changes need owner or staff | [venue-profile-api.md](venue-profile-api.md) |
| Moderation | `GET /venue/admin/unpublished`, `isModerationBlocked` on `PUT /venue/{id}` | Staff | [venue-profile-api.md](venue-profile-api.md) |
| Categories | `GET /venueCategory`, `PUT /venue/{id}/categories`, admin routes | Anonymous read; owner or staff set; staff manage | [venue-categories-api.md](venue-categories-api.md) |
| Tags | `GET /venueTag/suggest`, `PUT /venue/{id}/tags`, admin routes | Anonymous suggest; owner or staff set; staff merge and delete | [venue-tags-api.md](venue-tags-api.md) |
| Regions | `GET /region`, `PUT /venue/{id}/regions`, admin routes | Anonymous read; owner or staff set; staff manage | [regions-api.md](regions-api.md) |
| Owners | `GET /Linker_UserToVenue/byUserID/{userId}`, `POST/DELETE /Linker_UserToVenue/venue/{venueId}/owners...`, `admin/venues`, `admin/users` | Own list; staff for the rest | |
| Import | `POST /venue/admin/import` | Staff | [venue-import-api.md](venue-import-api.md) |
| Events | `GET /event?venueId=&upcoming=`, `GET /event/venue/{id}/manage`, `POST /event/venue/{id}`, publish, unpublish, cancel | Anonymous for published; owner or staff for the rest | [venue-events-api.md](venue-events-api.md) |

## Staff import

Staff can seed the directory in bulk (and, later, a crawler can) with `POST /api/venue/admin/import`: up to 200 rows per request, each
a name and street address, with optional event page, category and region hints. It is idempotent: sending a venue again never creates a
duplicate, and it never changes a venue that has an owner or is blocked. See [venue-import-api.md](venue-import-api.md) for the request,
the limits and the per-row results. A crawler should run as its own dedicated staff user so every import is attributed in the audit log.

## Roles and permissions

- **Owner**: a row in `Linker_UserToVenue`. Whoever registers a venue is its first owner. Staff can add and remove owners.
- **Staff**: a user with `Users.Moderator` set.
- A venue's owners can change its profile and the events the venue itself hosts. Events with an artist belong to that artist's users.
- Blocking a venue (`IsModerationBlocked`) hides it everywhere public. Only staff can set or clear it.

## Security rules

These were reviewed for story #340 and are covered by tests (`tests/17_`, `18_`, `19_`).

- **Who decides.** Staff status and the user come from the sign-in token, never from the request. A permission check always runs
  before the body is read or applied.
- **Anonymous access is explicit.** Only the public reads in the table above are open. A test lists them per controller and fails if one is added.
- **Hidden means hidden.** An unpublished or blocked venue, its contacts and address, and its events answer `404` to everyone but its
  managers and staff. Drafts and events in review are only readable by the people who manage them.
- **Contacts, links and addresses of a venue.** These tables are shared with artists, users and vendors, so the venue rules apply
  to the rows that belong to a venue:
  - private contacts and links are only returned to the venue's owners and staff (`includePrivate=true` on `GET /api/contact/venue/{id}`
    needs the venue's owner, and the contact link list leaves private venue links out for everyone else);
  - adding, changing, reordering, linking or deleting a venue's contacts needs the venue's owner;
  - a contact that belongs to an artist or user can't be linked to a venue, so a private contact can't be shown on a public page;
  - `GET /api/Address` (the whole list) is staff only, and changing or deleting an address that a venue uses needs the owners of every venue that uses it.
- **A venue can't borrow someone else's rows.** An owner can't change a venue's `AddressID` through `PUT /api/venue/{id}` (the address
  form copies a shared address first), or set its `ExternalLinkID` or `PhoneContactID` to another row. A new venue needs an unused address.
- **Input.** Text lengths, URLs (http and https only), slugs, state codes, zip codes and counts are validated on the server. Venue names,
  import rows and tags can't contain `<` or `>`. Rich text (venue and event descriptions) is stored as HTML and **sanitized when it is shown**.
- **Pictures.** The website's image routes check the venue before any change to a venue's folder, and refuse paths with `.` or `..` segments
  so the folder that is checked is always the folder that is changed.
- **Errors.** A database error is logged on the server and the caller gets a plain message, never table or constraint names.

Known gaps, outside the venue feature and left for a follow-up story: contacts, links and addresses that belong to **artists, users
and vendors** are still readable and editable without an ownership check (`/api/contact`, `/api/LinkerEntityToContact`, `/api/Address`);
`PUT` and `DELETE` on `/api/picture`, `/api/ExternalLink` and `/api/PhoneContact` need only a sign-in; `PUT /api/artist/byID/{id}` has no
ownership check; and the website's image routes do not protect artist, user, blog or listing media.

## Database

Run these scripts in order on a new database. Each can be run again safely, and each says which database to check first.

| Script | Adds |
| --- | --- |
| `324_venue_data_model.sql` | Venues and their addresses |
| `325_venue_categories.sql` | Categories |
| `326_venue_tags.sql` | Amenity tags |
| `327_regions.sql` | Shared regions, and the artist and venue links to them |
| `329_venue_ownership.sql` | Owner links |
| `332_venue_hosted_events.sql` | Optional artist on events, and an index for a venue's events |
| `333_venue_profile_cover_pictures.sql` | Active profile and cover pictures |
| `335_venue_directory_indexes.sql` | Indexes for the directory |
| `335_sample_venues.sql` / `335_sample_venues_remove.sql` | Optional sample venues (test data only) |

## Settings

The venue feature adds no new settings. Pictures use the existing Azure storage connection of the website (`AZURE_STORAGE_CONNECTION_STRING`),
and sign-in uses the existing token settings.

## Tests

```bash
dotnet test tests\tests.csproj -c Release
```

The venue tests are `tests/03_` to `tests/19_`:

| File | Covers |
| --- | --- |
| `03`, `04`, `05` | Venue reads and writes, ownership and permissions, owner assignment |
| `06`, `08` | Search and platform links (feed, comments, moderation), anonymous rules for venue reads |
| `07` | Staff import and its idempotency |
| `09`, `10`, `11` | Categories, tags, regions |
| `12`, `15` | Directory filters and paging, public page data |
| `13`, `14` | Profile, address and publishing, pictures |
| `16` | Artist regions and the migration |
| `17` | Events: who manages what, the publish lifecycle |
| `18` | The whole journey from registration to a listed venue with a published event, and anonymous access rules for every venue controller |
| `19` | Security: private contacts, links and addresses of a venue, borrowed address ids, events of a venue that is not public |
