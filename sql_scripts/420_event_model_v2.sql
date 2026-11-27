-- Story #420 (tag-web): event model v2
-- Events gain the time zone they happen in, a ticket sales window and how early check-in staff may start scanning.
-- Existing events keep working: the time zone stays empty, there is no sales limit, and check-in opens 60 minutes before.
-- Idempotent: safe to re-run. No existing data is changed.
--
-- Run against the right database first and check which server you are on:
--   SELECT current_database(), inet_server_addr();

BEGIN;

ALTER TABLE public."Events" ADD COLUMN IF NOT EXISTS "TimeZoneId" character varying(64) NULL;
ALTER TABLE public."Events" ADD COLUMN IF NOT EXISTS "SalesStart" timestamp with time zone NULL;
ALTER TABLE public."Events" ADD COLUMN IF NOT EXISTS "SalesEnd" timestamp with time zone NULL;
ALTER TABLE public."Events" ADD COLUMN IF NOT EXISTS "CheckInWindowMinutes" integer NOT NULL DEFAULT 60;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_Events_CheckInWindowMinutes') THEN
        ALTER TABLE public."Events"
            ADD CONSTRAINT "CK_Events_CheckInWindowMinutes" CHECK ("CheckInWindowMinutes" BETWEEN 0 AND 360);
    END IF;
END $$;

COMMIT;
