-- Story #335: indexes for the public venue directory (GET /api/venue/list)
-- The directory lists published venues alphabetically by name, pages with a keyset cursor, and filters by city, state,
-- zip prefix, category, tag and region. Idempotent: safe to re-run. No data changes.
--
-- Run against the right database first and check which server you are on:
--   SELECT current_database(), inet_server_addr();
--
-- Not indexed on purpose: the name search matches anywhere in the name (LIKE '%text%'), which an ordinary index can't serve.
-- It is fast enough while the directory holds thousands of venues. If it grows much larger, add a trigram index
-- (CREATE EXTENSION pg_trgm, then a GIN index with gin_trgm_ops on the same expression as below).

BEGIN;

-- Serves the alphabetical order and the keyset cursor, for published, non-blocked venues only.
-- The expression is the same one the API sorts by: lower(coalesce(plaintext name, name)).
CREATE INDEX IF NOT EXISTS "IX_Venues_Directory_NameSort"
    ON public."Venues" ((lower(COALESCE("Name_Plaintext", "Name"))), "VenueID")
    WHERE "IsPublished" AND NOT "IsModerationBlocked";

-- The join from a venue to its address.
CREATE INDEX IF NOT EXISTS "IX_Venues_AddressID"
    ON public."Venues" ("AddressID");

-- City and state are compared ignoring case.
CREATE INDEX IF NOT EXISTS "IX_Addresses_City_Lower"
    ON public."Addresses" (lower("City"));

CREATE INDEX IF NOT EXISTS "IX_Addresses_State_Lower"
    ON public."Addresses" (lower("State"));

-- Zip codes are matched by prefix (LIKE '303%'), which needs text_pattern_ops.
CREATE INDEX IF NOT EXISTS "IX_Addresses_ZipCode_Prefix"
    ON public."Addresses" ("ZipCode" text_pattern_ops);

-- The category, tag and region links already have indexes on their foreign keys from stories #325, #326 and #327.

COMMIT;
