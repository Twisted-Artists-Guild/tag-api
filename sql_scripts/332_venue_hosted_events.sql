-- Story #332: venues host and manage events
-- An event can now be hosted by a venue alone, so "Events"."ArtistID" may be empty. Artist-hosted events are unchanged.
-- Also adds an index for "events at this venue, ordered by start", which the venue page and venue portal both use.
-- Idempotent: safe to re-run. No data is changed.
--
-- Run against the right database first and check which server you are on:
--   SELECT current_database(), inet_server_addr();

BEGIN;

ALTER TABLE public."Events" ALTER COLUMN "ArtistID" DROP NOT NULL;

CREATE INDEX IF NOT EXISTS "IX_Events_VenueID_StartTime"
    ON public."Events" ("VenueID", "StartTime");

COMMIT;
