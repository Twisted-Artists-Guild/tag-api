-- Story #324: venue data model with optional owner, contacts, description and event-source URL
-- A venue can exist with only a name and an address (for example staff-seeded, unowned venues).
-- Adds a rich-text Description (with trigger-maintained plaintext copy) and an optional EventSourceUrl
-- that a future crawler can scan for events. Safe to re-run.

BEGIN;

-- 1) Website and phone are optional. AddressID stays required.
DO $migration$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'Venues' AND column_name = 'ExternalLinkID'
    ) THEN
        ALTER TABLE public."Venues" ALTER COLUMN "ExternalLinkID" DROP NOT NULL;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'Venues' AND column_name = 'PhoneContactID'
    ) THEN
        ALTER TABLE public."Venues" ALTER COLUMN "PhoneContactID" DROP NOT NULL;
    END IF;
END
$migration$;

-- 2) New columns.
ALTER TABLE public."Venues"
    ADD COLUMN IF NOT EXISTS "Description" text NULL,
    ADD COLUMN IF NOT EXISTS "Description_Plaintext" text NULL,
    ADD COLUMN IF NOT EXISTS "EventSourceUrl" varchar(2048) NULL;

-- 3) EventSourceUrl must be an absolute http or https URL when present.
DO $migration$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'CK_Venues_EventSourceUrl_HttpOrHttps' AND conrelid = 'public."Venues"'::regclass
    ) THEN
        ALTER TABLE public."Venues"
            ADD CONSTRAINT "CK_Venues_EventSourceUrl_HttpOrHttps"
            CHECK ("EventSourceUrl" IS NULL OR "EventSourceUrl" ~* '^https?://[^[:space:]]+$');
    END IF;
END
$migration$;

-- 4) Keep the plaintext copy of the description in sync, alongside the existing Name_Plaintext behavior.
CREATE OR REPLACE FUNCTION public.trg_set_venues_plaintext()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    NEW."Name_Plaintext" := public.fn_strip_html_to_plaintext(NEW."Name");
    NEW."Description_Plaintext" := public.fn_strip_html_to_plaintext(NEW."Description");
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS tr_venues_set_plaintext ON public."Venues";
CREATE TRIGGER tr_venues_set_plaintext
BEFORE INSERT OR UPDATE OF "Name", "Description"
ON public."Venues"
FOR EACH ROW
EXECUTE FUNCTION public.trg_set_venues_plaintext();

COMMIT;
