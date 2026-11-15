-- Story #335: removes the sample venues added by 335_sample_venues.sql.
-- Matches the exact sample venue names, so no other venue is touched. Links to categories, tags and regions are removed
-- with each venue. The sample addresses are removed too, and so are any amenity tags that no venue uses any more.
-- Idempotent: safe to re-run.
--
-- Run against the right database first and check which server you are on:
--   SELECT current_database(), inet_server_addr();

BEGIN;

CREATE TEMP TABLE sample_venue_names (name text PRIMARY KEY) ON COMMIT DROP;
INSERT INTO sample_venue_names (name) VALUES
    ('Sample Harbor Lights Theater'), ('Sample Peachtree Gallery'), ('Sample Midtown Music Club'),
    ('Sample Augusta Riverside Hall'), ('Sample Queen City Studio'), ('Sample Blue Ridge Convention Center'),
    ('Sample Music Row Listening Room'), ('Sample Riverfront Art Space'), ('Sample Orange Grove Theater'),
    ('Sample Biscayne Bar and Stage'), ('Sample Congress Avenue Gallery'), ('Sample Lone Star Convention Hall'),
    ('Sample Mission District Studio'), ('Sample Hudson Yards Loft'), ('Sample Lakefront Pavilion'),
    ('Sample Rainier Club House'), ('Sample Unpublished Venue'), ('Sample Blocked Venue');

CREATE TEMP TABLE sample_venue_addresses ON COMMIT DROP AS
SELECT v."AddressID"
FROM public."Venues" v
JOIN sample_venue_names n ON lower(v."Name") = lower(n.name);

DELETE FROM public."Linker_VenueToCategory"
WHERE "VenueID" IN (SELECT v."VenueID" FROM public."Venues" v JOIN sample_venue_names n ON lower(v."Name") = lower(n.name));

DELETE FROM public."Linker_VenueToTag"
WHERE "VenueID" IN (SELECT v."VenueID" FROM public."Venues" v JOIN sample_venue_names n ON lower(v."Name") = lower(n.name));

DELETE FROM public."Linker_VenueToRegion"
WHERE "VenueID" IN (SELECT v."VenueID" FROM public."Venues" v JOIN sample_venue_names n ON lower(v."Name") = lower(n.name));

DELETE FROM public."Venues" v
USING sample_venue_names n
WHERE lower(v."Name") = lower(n.name);

DELETE FROM public."Addresses"
WHERE "AddressID" IN (SELECT "AddressID" FROM sample_venue_addresses)
  AND NOT EXISTS (SELECT 1 FROM public."Venues" v WHERE v."AddressID" = public."Addresses"."AddressID");

-- Only the tags the sample script uses, and only when nothing else uses them.
DELETE FROM public."VenueTags" t
WHERE t."Name" IN ('stage', 'parking', 'wheelchair accessible', 'sound system', 'wifi', 'bar', 'green room', 'outdoor',
                   'kitchen', 'projector', 'loading dock', 'all ages')
  AND NOT EXISTS (SELECT 1 FROM public."Linker_VenueToTag" l WHERE l."VenueTagID" = t."VenueTagID");

COMMIT;

-- Check: expect 0.
SELECT count(*) AS sample_venues_left FROM public."Venues" WHERE "Name" LIKE 'Sample %';
