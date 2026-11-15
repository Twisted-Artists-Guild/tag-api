-- Story #333: active profile and cover pictures for venues
-- A venue already stores its logo picture (LogoPicID). This adds the active profile and cover pictures the same way artists
-- have them, so "Set Active" on a venue's profile and cover images has somewhere to save.
-- Idempotent: safe to re-run. Existing venues keep both columns empty until their owner sets an active picture; the public
-- page falls back to the newest file in the venue's profile and cover folders until then.
--
-- Run against the right database first and check which server you are on:
--   SELECT current_database(), inet_server_addr();

BEGIN;

ALTER TABLE public."Venues" ADD COLUMN IF NOT EXISTS "ProfilePicID" integer NULL;
ALTER TABLE public."Venues" ADD COLUMN IF NOT EXISTS "CoverPicID" integer NULL;

-- Deleting a picture clears the venue's pointer to it, like the logo and the artist pictures do.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Venues_Pictures_ProfilePicID') THEN
        ALTER TABLE public."Venues"
            ADD CONSTRAINT "FK_Venues_Pictures_ProfilePicID"
            FOREIGN KEY ("ProfilePicID") REFERENCES public."Pictures" ("PictureID") ON DELETE SET NULL;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Venues_Pictures_CoverPicID') THEN
        ALTER TABLE public."Venues"
            ADD CONSTRAINT "FK_Venues_Pictures_CoverPicID"
            FOREIGN KEY ("CoverPicID") REFERENCES public."Pictures" ("PictureID") ON DELETE SET NULL;
    END IF;
END
$$;

-- Keeps deleting a picture fast: the database checks these columns for every venue that might point at it.
CREATE INDEX IF NOT EXISTS "IX_Venues_ProfilePicID" ON public."Venues" ("ProfilePicID");
CREATE INDEX IF NOT EXISTS "IX_Venues_CoverPicID" ON public."Venues" ("CoverPicID");

COMMIT;

-- Check: both columns exist.
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'Venues' AND column_name IN ('ProfilePicID', 'CoverPicID')
ORDER BY column_name;
