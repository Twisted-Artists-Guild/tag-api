-- Story #193: logo support for Artist/Vendor/Venue/Event
-- Adds an active logo pointer per entity and a history table for restoring older versions.

BEGIN;

ALTER TABLE public."Artists"
    ADD COLUMN IF NOT EXISTS "LogoPicID" integer NULL;

ALTER TABLE public."Vendors"
    ADD COLUMN IF NOT EXISTS "LogoPicID" integer NULL;

ALTER TABLE public."Venues"
    ADD COLUMN IF NOT EXISTS "LogoPicID" integer NULL;

ALTER TABLE public."Events"
    ADD COLUMN IF NOT EXISTS "LogoPicID" integer NULL;

CREATE TABLE IF NOT EXISTS public."LogoHistory" (
    "LogoHistoryID" serial PRIMARY KEY,
    "EntityType" varchar(50) NOT NULL,
    "EntityID" integer NOT NULL,
    "PictureID" integer NOT NULL,
    "IsActive" boolean NOT NULL DEFAULT false,
    "IsArchived" boolean NOT NULL DEFAULT false,
    "CreatedUtc" timestamptz NOT NULL DEFAULT NOW(),
    "ArchivedUtc" timestamptz NULL,
    "RestoredUtc" timestamptz NULL,
    "ReplacedByHistoryID" integer NULL,
    "PreviousLogoHistoryID" integer NULL
);

CREATE INDEX IF NOT EXISTS "IX_LogoHistory_Entity_Active"
    ON public."LogoHistory" ("EntityType", "EntityID", "IsActive");

DO $migration$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LogoHistory_Pictures_PictureID' AND conrelid = 'public."LogoHistory"'::regclass) THEN
        ALTER TABLE public."LogoHistory"
            ADD CONSTRAINT "FK_LogoHistory_Pictures_PictureID"
            FOREIGN KEY ("PictureID") REFERENCES public."Pictures" ("PictureID") ON DELETE RESTRICT;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Artists_Pictures_LogoPicID' AND conrelid = 'public."Artists"'::regclass) THEN
        ALTER TABLE public."Artists"
            ADD CONSTRAINT "FK_Artists_Pictures_LogoPicID"
            FOREIGN KEY ("LogoPicID") REFERENCES public."Pictures" ("PictureID") ON DELETE SET NULL;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Vendors_Pictures_LogoPicID' AND conrelid = 'public."Vendors"'::regclass) THEN
        ALTER TABLE public."Vendors"
            ADD CONSTRAINT "FK_Vendors_Pictures_LogoPicID"
            FOREIGN KEY ("LogoPicID") REFERENCES public."Pictures" ("PictureID") ON DELETE SET NULL;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Venues_Pictures_LogoPicID' AND conrelid = 'public."Venues"'::regclass) THEN
        ALTER TABLE public."Venues"
            ADD CONSTRAINT "FK_Venues_Pictures_LogoPicID"
            FOREIGN KEY ("LogoPicID") REFERENCES public."Pictures" ("PictureID") ON DELETE SET NULL;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Events_Pictures_LogoPicID' AND conrelid = 'public."Events"'::regclass) THEN
        ALTER TABLE public."Events"
            ADD CONSTRAINT "FK_Events_Pictures_LogoPicID"
            FOREIGN KEY ("LogoPicID") REFERENCES public."Pictures" ("PictureID") ON DELETE SET NULL;
    END IF;
END
$migration$;

COMMIT;
