-- ============================================================
-- HTML vs Plaintext rollout
-- ============================================================
-- Purpose:
--   1) Add plaintext mirror columns for rich-text capable fields.
--   2) Create reusable HTML stripping function.
--   3) Add insert/update triggers to always keep plaintext mirrors current.
--   4) Backfill plaintext mirrors from existing richtext data.
--
-- Notes:
--   - Idempotent: safe to run multiple times.
--   - Plaintext columns are nullable and derived from richtext columns.
--   - If richtext is null/empty, plaintext is set to null.
-- ============================================================

BEGIN;

-- ============================================================
-- 1) Plaintext Columns
-- ============================================================

ALTER TABLE "Artists"
	ADD COLUMN IF NOT EXISTS "Title_Plaintext" text,
	ADD COLUMN IF NOT EXISTS "Byline_Plaintext" text,
	ADD COLUMN IF NOT EXISTS "Statement_Plaintext" text,
	ADD COLUMN IF NOT EXISTS "Biography_Plaintext" text;

ALTER TABLE "Listings"
	ADD COLUMN IF NOT EXISTS "Title_Plaintext" text,
	ADD COLUMN IF NOT EXISTS "Description_Plaintext" text;

ALTER TABLE "Blogs"
	ADD COLUMN IF NOT EXISTS "Title_Plaintext" text,
	ADD COLUMN IF NOT EXISTS "Byline_Plaintext" text,
	ADD COLUMN IF NOT EXISTS "Body_Plaintext" text;

ALTER TABLE "Events"
	ADD COLUMN IF NOT EXISTS "Title_Plaintext" text,
	ADD COLUMN IF NOT EXISTS "Description_Plaintext" text,
	ADD COLUMN IF NOT EXISTS "Note_Plaintext" text;

ALTER TABLE "Vendors"
	ADD COLUMN IF NOT EXISTS "CompanyName_Plaintext" text,
	ADD COLUMN IF NOT EXISTS "NotesOnContracts_Plaintext" text,
	ADD COLUMN IF NOT EXISTS "NotesOnVendors_Plaintext" text;

ALTER TABLE "Venues"
	ADD COLUMN IF NOT EXISTS "Name_Plaintext" text;

-- ============================================================
-- 2) HTML Strip Function
-- ============================================================

CREATE OR REPLACE FUNCTION public.fn_strip_html_to_plaintext(input_text text)
RETURNS text
LANGUAGE plpgsql
IMMUTABLE
AS $$
DECLARE
	cleaned text;
BEGIN
	IF input_text IS NULL THEN
		RETURN NULL;
	END IF;

	cleaned := input_text;

	-- Remove script/style payloads entirely.
	cleaned := regexp_replace(cleaned, '(?is)<(script|style)[^>]*>.*?</\1>', ' ', 'g');

	-- Convert common block/line-break tags to newlines first.
	cleaned := regexp_replace(cleaned, '(?i)<br\s*/?>', E'\n', 'g');
	cleaned := regexp_replace(cleaned, '(?i)</(p|div|li|ul|ol|h1|h2|h3|h4|h5|h6|tr|section|article|blockquote)>', E'\n', 'g');

	-- Remove remaining HTML tags.
	cleaned := regexp_replace(cleaned, '(?is)<[^>]+>', ' ', 'g');

	-- Decode common HTML entities.
	cleaned := replace(cleaned, '&nbsp;', ' ');
	cleaned := replace(cleaned, '&amp;', '&');
	cleaned := replace(cleaned, '&quot;', '"');
	cleaned := replace(cleaned, '&#39;', '''');
	cleaned := replace(cleaned, '&lt;', '<');
	cleaned := replace(cleaned, '&gt;', '>');

	-- Normalize whitespace.
	cleaned := regexp_replace(cleaned, E'[\t\r ]+', ' ', 'g');
	cleaned := regexp_replace(cleaned, E'\n\s*\n+', E'\n', 'g');
	cleaned := btrim(cleaned);

	RETURN NULLIF(cleaned, '');
END;
$$;

-- ============================================================
-- 3) Trigger Functions
-- ============================================================

CREATE OR REPLACE FUNCTION public.trg_set_artists_plaintext()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
	NEW."Title_Plaintext" := public.fn_strip_html_to_plaintext(NEW."Title");
	NEW."Byline_Plaintext" := public.fn_strip_html_to_plaintext(NEW."Byline");
	NEW."Statement_Plaintext" := public.fn_strip_html_to_plaintext(NEW."Statement");
	NEW."Biography_Plaintext" := public.fn_strip_html_to_plaintext(NEW."Biography");
	RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION public.trg_set_listings_plaintext()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
	NEW."Title_Plaintext" := public.fn_strip_html_to_plaintext(NEW."Title");
	NEW."Description_Plaintext" := public.fn_strip_html_to_plaintext(NEW."Description");
	RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION public.trg_set_blogs_plaintext()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
	NEW."Title_Plaintext" := public.fn_strip_html_to_plaintext(NEW."Title");
	NEW."Byline_Plaintext" := public.fn_strip_html_to_plaintext(NEW."Byline");
	NEW."Body_Plaintext" := public.fn_strip_html_to_plaintext(NEW."Body");
	RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION public.trg_set_events_plaintext()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
	NEW."Title_Plaintext" := public.fn_strip_html_to_plaintext(NEW."Title");
	NEW."Description_Plaintext" := public.fn_strip_html_to_plaintext(NEW."Description");
	NEW."Note_Plaintext" := public.fn_strip_html_to_plaintext(NEW."Note");
	RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION public.trg_set_vendors_plaintext()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
	NEW."CompanyName_Plaintext" := public.fn_strip_html_to_plaintext(NEW."CompanyName");
	NEW."NotesOnContracts_Plaintext" := public.fn_strip_html_to_plaintext(NEW."NotesOnContracts");
	NEW."NotesOnVendors_Plaintext" := public.fn_strip_html_to_plaintext(NEW."NotesOnVendors");
	RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION public.trg_set_venues_plaintext()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
	NEW."Name_Plaintext" := public.fn_strip_html_to_plaintext(NEW."Name");
	RETURN NEW;
END;
$$;

-- ============================================================
-- 4) Triggers
-- ============================================================

DROP TRIGGER IF EXISTS tr_artists_set_plaintext ON "Artists";
CREATE TRIGGER tr_artists_set_plaintext
BEFORE INSERT OR UPDATE OF "Title", "Byline", "Statement", "Biography"
ON "Artists"
FOR EACH ROW
EXECUTE FUNCTION public.trg_set_artists_plaintext();

DROP TRIGGER IF EXISTS tr_listings_set_plaintext ON "Listings";
CREATE TRIGGER tr_listings_set_plaintext
BEFORE INSERT OR UPDATE OF "Title", "Description"
ON "Listings"
FOR EACH ROW
EXECUTE FUNCTION public.trg_set_listings_plaintext();

DROP TRIGGER IF EXISTS tr_blogs_set_plaintext ON "Blogs";
CREATE TRIGGER tr_blogs_set_plaintext
BEFORE INSERT OR UPDATE OF "Title", "Byline", "Body"
ON "Blogs"
FOR EACH ROW
EXECUTE FUNCTION public.trg_set_blogs_plaintext();

DROP TRIGGER IF EXISTS tr_events_set_plaintext ON "Events";
CREATE TRIGGER tr_events_set_plaintext
BEFORE INSERT OR UPDATE OF "Title", "Description", "Note"
ON "Events"
FOR EACH ROW
EXECUTE FUNCTION public.trg_set_events_plaintext();

DROP TRIGGER IF EXISTS tr_vendors_set_plaintext ON "Vendors";
CREATE TRIGGER tr_vendors_set_plaintext
BEFORE INSERT OR UPDATE OF "CompanyName", "NotesOnContracts", "NotesOnVendors"
ON "Vendors"
FOR EACH ROW
EXECUTE FUNCTION public.trg_set_vendors_plaintext();

DROP TRIGGER IF EXISTS tr_venues_set_plaintext ON "Venues";
CREATE TRIGGER tr_venues_set_plaintext
BEFORE INSERT OR UPDATE OF "Name"
ON "Venues"
FOR EACH ROW
EXECUTE FUNCTION public.trg_set_venues_plaintext();

-- ============================================================
-- 5) Backfill Existing Data
-- ============================================================

UPDATE "Artists"
SET
	"Title_Plaintext" = public.fn_strip_html_to_plaintext("Title"),
	"Byline_Plaintext" = public.fn_strip_html_to_plaintext("Byline"),
	"Statement_Plaintext" = public.fn_strip_html_to_plaintext("Statement"),
	"Biography_Plaintext" = public.fn_strip_html_to_plaintext("Biography")
WHERE
	COALESCE(NULLIF(BTRIM("Title_Plaintext"), ''), '') = ''
	OR COALESCE(NULLIF(BTRIM("Byline_Plaintext"), ''), '') = ''
	OR COALESCE(NULLIF(BTRIM("Statement_Plaintext"), ''), '') = ''
	OR COALESCE(NULLIF(BTRIM("Biography_Plaintext"), ''), '') = '';

UPDATE "Listings"
SET
	"Title_Plaintext" = public.fn_strip_html_to_plaintext("Title"),
	"Description_Plaintext" = public.fn_strip_html_to_plaintext("Description")
WHERE
	COALESCE(NULLIF(BTRIM("Title_Plaintext"), ''), '') = ''
	OR COALESCE(NULLIF(BTRIM("Description_Plaintext"), ''), '') = '';

UPDATE "Blogs"
SET
	"Title_Plaintext" = public.fn_strip_html_to_plaintext("Title"),
	"Byline_Plaintext" = public.fn_strip_html_to_plaintext("Byline"),
	"Body_Plaintext" = public.fn_strip_html_to_plaintext("Body")
WHERE
	COALESCE(NULLIF(BTRIM("Title_Plaintext"), ''), '') = ''
	OR COALESCE(NULLIF(BTRIM("Byline_Plaintext"), ''), '') = ''
	OR COALESCE(NULLIF(BTRIM("Body_Plaintext"), ''), '') = '';

UPDATE "Events"
SET
	"Title_Plaintext" = public.fn_strip_html_to_plaintext("Title"),
	"Description_Plaintext" = public.fn_strip_html_to_plaintext("Description"),
	"Note_Plaintext" = public.fn_strip_html_to_plaintext("Note")
WHERE
	COALESCE(NULLIF(BTRIM("Title_Plaintext"), ''), '') = ''
	OR COALESCE(NULLIF(BTRIM("Description_Plaintext"), ''), '') = ''
	OR COALESCE(NULLIF(BTRIM("Note_Plaintext"), ''), '') = '';

UPDATE "Vendors"
SET
	"CompanyName_Plaintext" = public.fn_strip_html_to_plaintext("CompanyName"),
	"NotesOnContracts_Plaintext" = public.fn_strip_html_to_plaintext("NotesOnContracts"),
	"NotesOnVendors_Plaintext" = public.fn_strip_html_to_plaintext("NotesOnVendors")
WHERE
	COALESCE(NULLIF(BTRIM("CompanyName_Plaintext"), ''), '') = ''
	OR COALESCE(NULLIF(BTRIM("NotesOnContracts_Plaintext"), ''), '') = ''
	OR COALESCE(NULLIF(BTRIM("NotesOnVendors_Plaintext"), ''), '') = '';

UPDATE "Venues"
SET
	"Name_Plaintext" = public.fn_strip_html_to_plaintext("Name")
WHERE
	COALESCE(NULLIF(BTRIM("Name_Plaintext"), ''), '') = '';

COMMIT;
