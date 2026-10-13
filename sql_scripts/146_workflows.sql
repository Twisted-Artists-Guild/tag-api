-- Run this script against the TAG database after backing up.
-- Purpose: enforce uniqueness for Picture.NormalizedURL and support URL-based picture lookups.

BEGIN;

UPDATE "Pictures"
SET "NormalizedURL" = lower(trim("URL"))
WHERE ("NormalizedURL" IS NULL OR trim("NormalizedURL") = '')
  AND "URL" IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Pictures_NormalizedURL"
ON "Pictures" ("NormalizedURL")
WHERE "NormalizedURL" IS NOT NULL;

COMMIT;


-- Run this script against the TAG database after backing up.
-- Purpose: store workflow step definitions in DB so required publish steps and follow-up tutorials
--          are configuration-driven instead of hardcoded in API code.

BEGIN;

CREATE TABLE IF NOT EXISTS "WorkflowStepDefinitions"
(
    "WorkflowStepDefinitionID" bigserial PRIMARY KEY,
    "EntityType" character varying(50) NOT NULL,
    "WorkflowName" character varying(100) NOT NULL DEFAULT 'default',
    "StepKey" character varying(80) NOT NULL,
    "StepLabel" character varying(160) NOT NULL,
    "StepOrder" integer NOT NULL,
    "IsRequiredForPublish" boolean NOT NULL DEFAULT false,
    "IsPostPublish" boolean NOT NULL DEFAULT false,
    "Notes" text NULL
);

ALTER TABLE "WorkflowStepDefinitions"
    DROP CONSTRAINT IF EXISTS "CK_WorkflowStepDefinitions_ValidEntityType";

ALTER TABLE "WorkflowStepDefinitions"
    ADD CONSTRAINT "CK_WorkflowStepDefinitions_ValidEntityType"
    CHECK ("EntityType" IN ('user', 'artist', 'venue', 'vendor', 'listing'));

CREATE UNIQUE INDEX IF NOT EXISTS "UX_WorkflowStepDefinitions_Entity_Workflow_Step"
    ON "WorkflowStepDefinitions" ("EntityType", "WorkflowName", "StepKey");

CREATE INDEX IF NOT EXISTS "IX_WorkflowStepDefinitions_Entity_Workflow_Order"
    ON "WorkflowStepDefinitions" ("EntityType", "WorkflowName", "StepOrder");

-- User registration workflow aligned to current /join/user forms.
INSERT INTO "WorkflowStepDefinitions"
("EntityType", "WorkflowName", "StepKey", "StepLabel", "StepOrder", "IsRequiredForPublish", "IsPostPublish", "Notes")
VALUES
    ('user', 'default', 'accepted_tc', 'Accepted Terms', 10, TRUE, FALSE, 'Step 1 in /join/user'),
    ('user', 'default', 'reserved_slug', 'Reserved Username', 20, TRUE, FALSE, 'Step 2 in /join/user'),
    ('user', 'default', 'completed_profile_core', 'Completed Core Profile', 30, TRUE, FALSE, 'Step 3 in /join/user/[slug]'),
    ('user', 'default', 'completed_primary_contacts', 'Completed Primary Contacts', 40, TRUE, FALSE, 'Step 4 in /join/user/[slug]'),
    ('user', 'default', 'completed_privacy', 'Completed Privacy', 50, TRUE, FALSE, 'Step 5 in /join/user/[slug]'),
    ('user', 'default', 'completed_media', 'Completed Media', 60, TRUE, FALSE, 'Step 6 in /join/user/[slug]'),
    ('user', 'default', 'completed_preferences', 'Completed Preferences', 70, TRUE, FALSE, 'Step 7 in /join/user/[slug]'),
    ('user', 'default', 'published', 'Published', 80, FALSE, TRUE, 'Publish action should be represented as a workflow step'),
    ('user', 'default', 'tutorial_bloomscroll', 'Tutorial: Bloomscroll', 90, FALSE, TRUE, 'User follow-up tutorial'),
    ('user', 'default', 'tutorial_contest_voting', 'Tutorial: Contest Voting', 100, FALSE, TRUE, 'User follow-up tutorial'),
    ('user', 'default', 'completed_followup_form', 'Completed Follow-up Form', 110, FALSE, TRUE, 'Requested user follow-up form'),
    ('user', 'default', 'first_post', 'First Post', 120, FALSE, TRUE, 'Universal first-post milestone')
ON CONFLICT ("EntityType", "WorkflowName", "StepKey") DO UPDATE
SET
    "StepLabel" = EXCLUDED."StepLabel",
    "StepOrder" = EXCLUDED."StepOrder",
    "IsRequiredForPublish" = EXCLUDED."IsRequiredForPublish",
    "IsPostPublish" = EXCLUDED."IsPostPublish",
    "Notes" = EXCLUDED."Notes";

-- Artist registration workflow aligned to current /join/artist forms.
INSERT INTO "WorkflowStepDefinitions"
("EntityType", "WorkflowName", "StepKey", "StepLabel", "StepOrder", "IsRequiredForPublish", "IsPostPublish", "Notes")
VALUES
    ('artist', 'default', 'accepted_tc', 'Accepted Terms', 10, TRUE, FALSE, 'Step 1 in /join/artist'),
    ('artist', 'default', 'reserved_slug', 'Reserved Slug', 20, TRUE, FALSE, 'Step 2 in /join/artist'),
    ('artist', 'default', 'added_bio', 'Completed Profile', 30, TRUE, FALSE, 'Step 3 in /join/artist/[slug]'),
    ('artist', 'default', 'private_contacts', 'Completed Primary Contacts', 40, TRUE, FALSE, 'Step 4 in /join/artist/[slug]'),
    ('artist', 'default', 'uploaded_photos', 'Completed Media', 50, TRUE, FALSE, 'Step 5 in /join/artist/[slug]'),
    ('artist', 'default', 'added_contacts', 'Completed Public Contacts', 60, TRUE, FALSE, 'Step 6 in /join/artist/[slug]'),
    ('artist', 'default', 'published', 'Published', 70, FALSE, TRUE, 'Publish action should be represented as a workflow step'),
    ('artist', 'default', 'tutorial_first_listing', 'Tutorial: First Listing', 80, FALSE, TRUE, 'Artist tutorial for how to list an item'),
    ('artist', 'default', 'first_post', 'First Post', 90, FALSE, TRUE, 'Universal first-post milestone')
ON CONFLICT ("EntityType", "WorkflowName", "StepKey") DO UPDATE
SET
    "StepLabel" = EXCLUDED."StepLabel",
    "StepOrder" = EXCLUDED."StepOrder",
    "IsRequiredForPublish" = EXCLUDED."IsRequiredForPublish",
    "IsPostPublish" = EXCLUDED."IsPostPublish",
    "Notes" = EXCLUDED."Notes";

-- Vendor registration workflow aligned to current /join/vendor forms.
INSERT INTO "WorkflowStepDefinitions"
("EntityType", "WorkflowName", "StepKey", "StepLabel", "StepOrder", "IsRequiredForPublish", "IsPostPublish", "Notes")
VALUES
    ('vendor', 'default', 'accepted_tc', 'Accepted Terms', 10, TRUE, FALSE, 'Step 1 in /join/vendor'),
    ('vendor', 'default', 'reserved_slug', 'Reserved Slug', 20, TRUE, FALSE, 'Step 2 in /join/vendor'),
    ('vendor', 'default', 'completed_business_details', 'Completed Business Details', 30, TRUE, FALSE, 'Step 3 in /join/vendor/[slug]'),
    ('vendor', 'default', 'completed_primary_contacts', 'Completed Primary Contacts', 40, TRUE, FALSE, 'Step 4 in /join/vendor/[slug]'),
    ('vendor', 'default', 'completed_media', 'Completed Media', 50, TRUE, FALSE, 'Step 5 in /join/vendor/[slug]'),
    ('vendor', 'default', 'completed_public_contacts', 'Completed Public Contacts', 60, TRUE, FALSE, 'Step 6 in /join/vendor/[slug]'),
    ('vendor', 'default', 'published', 'Published', 70, FALSE, TRUE, 'Publish action should be represented as a workflow step'),
    ('vendor', 'default', 'first_post', 'First Post', 80, FALSE, TRUE, 'Universal first-post milestone')
ON CONFLICT ("EntityType", "WorkflowName", "StepKey") DO UPDATE
SET
    "StepLabel" = EXCLUDED."StepLabel",
    "StepOrder" = EXCLUDED."StepOrder",
    "IsRequiredForPublish" = EXCLUDED."IsRequiredForPublish",
    "IsPostPublish" = EXCLUDED."IsPostPublish",
    "Notes" = EXCLUDED."Notes";

-- Venue registration workflow aligned to current /join/venue forms.
INSERT INTO "WorkflowStepDefinitions"
("EntityType", "WorkflowName", "StepKey", "StepLabel", "StepOrder", "IsRequiredForPublish", "IsPostPublish", "Notes")
VALUES
    ('venue', 'default', 'accepted_tc', 'Accepted Terms', 10, TRUE, FALSE, 'Step 1 in /join/venue'),
    ('venue', 'default', 'reserved_slug', 'Reserved Slug', 20, TRUE, FALSE, 'Step 2 in /join/venue'),
    ('venue', 'default', 'completed_venue_profile', 'Completed Venue Profile', 30, TRUE, FALSE, 'Step 3 in /join/venue/[slug]'),
    ('venue', 'default', 'completed_primary_contacts', 'Completed Primary Contacts', 40, TRUE, FALSE, 'Step 4 in /join/venue/[slug]'),
    ('venue', 'default', 'completed_media', 'Completed Media', 50, TRUE, FALSE, 'Step 5 in /join/venue/[slug]'),
    ('venue', 'default', 'completed_public_contacts', 'Completed Public Contacts', 60, TRUE, FALSE, 'Step 6 in /join/venue/[slug]'),
    ('venue', 'default', 'published', 'Published', 70, FALSE, TRUE, 'Publish action should be represented as a workflow step'),
    ('venue', 'default', 'first_post', 'First Post', 80, FALSE, TRUE, 'Universal first-post milestone')
ON CONFLICT ("EntityType", "WorkflowName", "StepKey") DO UPDATE
SET
    "StepLabel" = EXCLUDED."StepLabel",
    "StepOrder" = EXCLUDED."StepOrder",
    "IsRequiredForPublish" = EXCLUDED."IsRequiredForPublish",
    "IsPostPublish" = EXCLUDED."IsPostPublish",
    "Notes" = EXCLUDED."Notes";

COMMIT;


-- Run this script against the TAG database after backing up.
-- Purpose: remove deprecated ArtistMember from Users now that membership is tracked elsewhere.

BEGIN;

ALTER TABLE "Users"
    DROP COLUMN IF EXISTS "ArtistMember";

COMMIT;


-- Run this script against the TAG database after backing up.
-- Purpose: add publish/moderation flags and separate workflow tables per entity type.

BEGIN;

ALTER TABLE "Users"
    ADD COLUMN IF NOT EXISTS "IsPublished" boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS "IsModerationBlocked" boolean NOT NULL DEFAULT false;

ALTER TABLE "Artists"
    ADD COLUMN IF NOT EXISTS "IsPublished" boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS "IsModerationBlocked" boolean NOT NULL DEFAULT false;

ALTER TABLE "Venues"
    ADD COLUMN IF NOT EXISTS "IsPublished" boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS "IsModerationBlocked" boolean NOT NULL DEFAULT false;

ALTER TABLE "Vendors"
    ADD COLUMN IF NOT EXISTS "IsPublished" boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS "IsModerationBlocked" boolean NOT NULL DEFAULT false;

ALTER TABLE "Listings"
    ADD COLUMN IF NOT EXISTS "IsPublished" boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS "IsModerationBlocked" boolean NOT NULL DEFAULT false;

CREATE TABLE IF NOT EXISTS "UserWorkflows"
(
    "UserWorkflowID" bigserial PRIMARY KEY,
    "UserID" integer NOT NULL,
    "StepKey" character varying(80) NOT NULL,
    "IsCompleted" boolean NOT NULL DEFAULT false,
    "CompletedAt" timestamp with time zone NULL,
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedByUserID" integer NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserWorkflows_UserID_StepKey"
    ON "UserWorkflows" ("UserID", "StepKey");

CREATE INDEX IF NOT EXISTS "IX_UserWorkflows_UserID_IsCompleted"
    ON "UserWorkflows" ("UserID", "IsCompleted");

CREATE TABLE IF NOT EXISTS "ArtistWorkflows"
(
    "ArtistWorkflowID" bigserial PRIMARY KEY,
    "ArtistID" integer NOT NULL,
    "StepKey" character varying(80) NOT NULL,
    "IsCompleted" boolean NOT NULL DEFAULT false,
    "CompletedAt" timestamp with time zone NULL,
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedByUserID" integer NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_ArtistWorkflows_ArtistID_StepKey"
    ON "ArtistWorkflows" ("ArtistID", "StepKey");

CREATE INDEX IF NOT EXISTS "IX_ArtistWorkflows_ArtistID_IsCompleted"
    ON "ArtistWorkflows" ("ArtistID", "IsCompleted");

CREATE TABLE IF NOT EXISTS "VenueWorkflows"
(
    "VenueWorkflowID" bigserial PRIMARY KEY,
    "VenueID" integer NOT NULL,
    "StepKey" character varying(80) NOT NULL,
    "IsCompleted" boolean NOT NULL DEFAULT false,
    "CompletedAt" timestamp with time zone NULL,
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedByUserID" integer NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_VenueWorkflows_VenueID_StepKey"
    ON "VenueWorkflows" ("VenueID", "StepKey");

CREATE INDEX IF NOT EXISTS "IX_VenueWorkflows_VenueID_IsCompleted"
    ON "VenueWorkflows" ("VenueID", "IsCompleted");

CREATE TABLE IF NOT EXISTS "VendorWorkflows"
(
    "VendorWorkflowID" bigserial PRIMARY KEY,
    "VendorID" integer NOT NULL,
    "StepKey" character varying(80) NOT NULL,
    "IsCompleted" boolean NOT NULL DEFAULT false,
    "CompletedAt" timestamp with time zone NULL,
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedByUserID" integer NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_VendorWorkflows_VendorID_StepKey"
    ON "VendorWorkflows" ("VendorID", "StepKey");

CREATE INDEX IF NOT EXISTS "IX_VendorWorkflows_VendorID_IsCompleted"
    ON "VendorWorkflows" ("VendorID", "IsCompleted");

CREATE TABLE IF NOT EXISTS "ListingWorkflows"
(
    "ListingWorkflowID" bigserial PRIMARY KEY,
    "ListingID" integer NOT NULL,
    "StepKey" character varying(80) NOT NULL,
    "IsCompleted" boolean NOT NULL DEFAULT false,
    "CompletedAt" timestamp with time zone NULL,
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedByUserID" integer NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_ListingWorkflows_ListingID_StepKey"
    ON "ListingWorkflows" ("ListingID", "StepKey");

CREATE INDEX IF NOT EXISTS "IX_ListingWorkflows_ListingID_IsCompleted"
    ON "ListingWorkflows" ("ListingID", "IsCompleted");

COMMIT;


-- Run this compatibility block when API code still references UnifiedWorkflows.
-- Purpose: avoid runtime 42P01 errors while per-entity workflow tables are being adopted.

BEGIN;

CREATE TABLE IF NOT EXISTS "UnifiedWorkflows"
(
    "UnifiedWorkflowID" bigserial PRIMARY KEY,
    "EntityID" integer NOT NULL,
    "EntityType" character varying(50) NOT NULL,
    "WorkflowName" character varying(100) NOT NULL DEFAULT 'default',
    "StepKey" character varying(80) NOT NULL,
    "IsCompleted" boolean NOT NULL DEFAULT false,
    "CompletedAt" timestamp with time zone NULL,
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedByUserID" integer NULL
);

ALTER TABLE "UnifiedWorkflows"
    DROP CONSTRAINT IF EXISTS "CK_UnifiedWorkflow_ValidEntityType";

ALTER TABLE "UnifiedWorkflows"
    ADD CONSTRAINT "CK_UnifiedWorkflow_ValidEntityType"
    CHECK ("EntityType" IN ('user', 'artist', 'venue', 'vendor', 'listing'));

DROP INDEX IF EXISTS "IX_UnifiedWorkflows_EntityID_EntityType_WorkflowName";

CREATE UNIQUE INDEX IF NOT EXISTS "IX_UnifiedWorkflows_Entity_Workflow_Step"
    ON "UnifiedWorkflows" ("EntityID", "EntityType", "WorkflowName", "StepKey");

CREATE INDEX IF NOT EXISTS "IX_UnifiedWorkflows_Entity_Workflow"
    ON "UnifiedWorkflows" ("EntityID", "EntityType", "WorkflowName");

CREATE INDEX IF NOT EXISTS "IX_UnifiedWorkflows_Entity_IsCompleted"
    ON "UnifiedWorkflows" ("EntityID", "EntityType", "IsCompleted");

COMMIT;



-- Run this script against the TAG database after backing up.
-- Purpose: add legal-entity metadata columns to Artist for step-1 registration.

BEGIN;

ALTER TABLE "Artists"
    ADD COLUMN IF NOT EXISTS "Country" character varying(120),
    ADD COLUMN IF NOT EXISTS "StateOrProvince" character varying(120),
    ADD COLUMN IF NOT EXISTS "BusinessEntityType" character varying(80),
    ADD COLUMN IF NOT EXISTS "IsFormallyIncorporated" boolean;

COMMIT;

-- Run this script against the TAG database after backing up.
-- Purpose: expand DB-driven DynaForm coverage for ArtistForm1 (Forms_MetadataID=3)
--          and UserForm1 (Forms_MetadataID=9) without hardcoding fields in the web app.

BEGIN;

-- Keep metadata copy aligned with the expanded scope text.
UPDATE "Forms_Metadata"
SET
    "FormBody" = 'Fill out the information below to create or update your artist profile. Title is required; additional profile and linkage fields are optional and DB-driven.'
WHERE "Forms_MetadataID" = 3;

UPDATE "Forms_Metadata"
SET
    "FormBody" = 'Enter your user information. Username and email are required; optional profile/privacy fields are DB-driven from Forms_Fields.'
WHERE "Forms_MetadataID" = 9;

DELETE FROM "Forms_Fields"
WHERE "Forms_FieldID" = 110
   OR ("Forms_MetadataID" = 3 AND lower("Name") = 'primarycontactid');

-- ArtistForm1 expansion (near-complete editable coverage of Artist table profile fields).
INSERT INTO "Forms_Fields"
("Forms_FieldID", "Forms_MetadataID", "Name", "Type", "Label", "Placeholder", "DefaultValue", "FieldOrder", "Hidden", "IsRequired", "IsReadOnly", "ClassName", "Width", "Height", "RegexValidationPattern")
VALUES
    (108, 3, 'biography', 'textarea', 'Biography', 'Artist biography / background', '', 8, FALSE, FALSE, FALSE, 'textarea textarea-bordered w-full', '100%', '6', NULL),
    (109, 3, 'galleryid', 'number', 'Gallery ID', 'Linked gallery ID (optional)', '', 9, FALSE, FALSE, FALSE, 'input input-bordered w-full', '100%', NULL, 'validate_number'),
    (111, 3, 'profilepicid', 'number', 'Profile Picture ID', 'Picture ID for profile image', '', 10, FALSE, FALSE, FALSE, 'input input-bordered w-full', '100%', NULL, 'validate_number'),
    (112, 3, 'coverpicid', 'number', 'Cover Picture ID', 'Picture ID for cover image', '', 11, FALSE, FALSE, FALSE, 'input input-bordered w-full', '100%', NULL, 'validate_number')
ON CONFLICT ("Forms_FieldID") DO UPDATE
SET
    "Forms_MetadataID" = EXCLUDED."Forms_MetadataID",
    "Name" = EXCLUDED."Name",
    "Type" = EXCLUDED."Type",
    "Label" = EXCLUDED."Label",
    "Placeholder" = EXCLUDED."Placeholder",
    "DefaultValue" = EXCLUDED."DefaultValue",
    "FieldOrder" = EXCLUDED."FieldOrder",
    "Hidden" = EXCLUDED."Hidden",
    "IsRequired" = EXCLUDED."IsRequired",
    "IsReadOnly" = EXCLUDED."IsReadOnly",
    "ClassName" = EXCLUDED."ClassName",
    "Width" = EXCLUDED."Width",
    "Height" = EXCLUDED."Height",
    "RegexValidationPattern" = EXCLUDED."RegexValidationPattern";

-- UserForm1 expansion (near-complete editable profile/privacy coverage of User table).
INSERT INTO "Forms_Fields"
("Forms_FieldID", "Forms_MetadataID", "Name", "Type", "Label", "Placeholder", "DefaultValue", "FieldOrder", "Hidden", "IsRequired", "IsReadOnly", "ClassName", "Width", "Height", "RegexValidationPattern")
VALUES
    (412, 9, 'birthdate', 'date', 'Birth Date', 'YYYY-MM-DD', '', 12, FALSE, FALSE, FALSE, 'input input-bordered w-full', '100%', NULL, NULL),
    (413, 9, 'deathdate', 'date', 'Death Date', 'YYYY-MM-DD', '', 13, FALSE, FALSE, FALSE, 'input input-bordered w-full', '100%', NULL, NULL),
    (414, 9, 'hidefrompublic', 'checkbox', 'Hide Profile From Public', '', 'false', 14, FALSE, FALSE, FALSE, 'checkbox', '100%', NULL, NULL),
    (415, 9, 'joined', 'datetime', 'Joined', '', '', 15, TRUE, FALSE, TRUE, 'input input-bordered w-full', '100%', NULL, NULL)
ON CONFLICT ("Forms_FieldID") DO UPDATE
SET
    "Forms_MetadataID" = EXCLUDED."Forms_MetadataID",
    "Name" = EXCLUDED."Name",
    "Type" = EXCLUDED."Type",
    "Label" = EXCLUDED."Label",
    "Placeholder" = EXCLUDED."Placeholder",
    "DefaultValue" = EXCLUDED."DefaultValue",
    "FieldOrder" = EXCLUDED."FieldOrder",
    "Hidden" = EXCLUDED."Hidden",
    "IsRequired" = EXCLUDED."IsRequired",
    "IsReadOnly" = EXCLUDED."IsReadOnly",
    "ClassName" = EXCLUDED."ClassName",
    "Width" = EXCLUDED."Width",
    "Height" = EXCLUDED."Height",
    "RegexValidationPattern" = EXCLUDED."RegexValidationPattern";

COMMIT;


-- Run this script against the TAG database after backing up.
-- Purpose: add IncorporatedYear column to Artists table to record the year a formally incorporated entity was established.

BEGIN;

ALTER TABLE "Artists"
    ADD COLUMN IF NOT EXISTS "IncorporatedYear" integer;

COMMENT ON COLUMN "Artists"."IncorporatedYear" IS 'Four-digit year the artist entity was formally incorporated. Null for unincorporated or unknown.';

COMMIT;


-- Run this script against the TAG database after backing up.
-- Purpose: replace legacy privacy/primary contact flags with linker-scoped contact ownership.

BEGIN;

ALTER TABLE "Linker_EntityToContacts"
    ADD COLUMN IF NOT EXISTS "Scope" smallint;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'Linker_EntityToContacts'
          AND column_name = 'Scope'
          AND udt_name = 'ContactScope') THEN
        -- Drop potentially enum-typed index predicates before changing Scope to smallint.
        DROP INDEX IF EXISTS "IX_Linker_EntityToContacts_Scope";
        DROP INDEX IF EXISTS "UX_Linker_EntityToContacts_Artist_Primary";
        DROP INDEX IF EXISTS "UX_Linker_EntityToContacts_User_Primary";
        DROP INDEX IF EXISTS "UX_Linker_EntityToContacts_Venue_Primary";
        DROP INDEX IF EXISTS "UX_Linker_EntityToContacts_Vendor_Primary";

        ALTER TABLE "Linker_EntityToContacts"
            ALTER COLUMN "Scope" DROP DEFAULT;

        ALTER TABLE "Linker_EntityToContacts"
            ALTER COLUMN "Scope" TYPE smallint
            USING CASE
                WHEN "Scope"::text = 'private' THEN 0
                WHEN "Scope"::text = 'primary' THEN 1
                ELSE 2
            END;
    END IF;
END $$;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'Artists'
          AND column_name = 'PrimaryContactID') THEN
        EXECUTE '
            UPDATE "Linker_EntityToContacts" l
            SET "Scope" = 1
            FROM "Artists" a
            WHERE l."ArtistID" = a."ArtistID"
              AND l."ContactID" = a."PrimaryContactID"
              AND a."PrimaryContactID" IS NOT NULL
              AND l."Scope" IS NULL';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'Users'
          AND column_name = 'PrimaryContactID') THEN
        EXECUTE '
            UPDATE "Linker_EntityToContacts" l
            SET "Scope" = 1
            FROM "Users" u
            WHERE l."UserID" = u."UserID"
              AND l."ContactID" = u."PrimaryContactID"
              AND u."PrimaryContactID" IS NOT NULL
              AND l."Scope" IS NULL';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'Vendors'
          AND column_name = 'PrimaryContactID') THEN
        EXECUTE '
            UPDATE "Linker_EntityToContacts" l
            SET "Scope" = 1
            FROM "Vendors" v
            WHERE l."VendorID" = v."VendorID"
              AND l."ContactID" = v."PrimaryContactID"
              AND v."PrimaryContactID" IS NOT NULL
              AND l."Scope" IS NULL';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'Venues'
          AND column_name = 'PrimaryContactID') THEN
        EXECUTE '
            UPDATE "Linker_EntityToContacts" l
            SET "Scope" = 1
            FROM "Venues" v
            WHERE l."VenueID" = v."VenueID"
              AND l."ContactID" = v."PrimaryContactID"
              AND v."PrimaryContactID" IS NOT NULL
              AND l."Scope" IS NULL';
    END IF;
END $$;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'Contacts'
          AND column_name = 'IsPrivate') THEN
        IF EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'Linker_EntityToContacts'
              AND column_name = 'MakePublic') THEN
            EXECUTE '
                UPDATE "Linker_EntityToContacts" l
                SET "Scope" = 0
                FROM "Contacts" c
                LEFT JOIN "Addresses" a ON a."AddressID" = c."AddressID"
                LEFT JOIN "PhoneContacts" p ON p."PhoneContactID" = c."PhoneContactID"
                WHERE l."ContactID" = c."ContactID"
                  AND l."Scope" IS NULL
                  AND (
                      COALESCE(c."IsPrivate", FALSE)
                      OR COALESCE(a."IsPrivate", FALSE)
                      OR COALESCE(p."IsPrivate", FALSE)
                      OR NOT COALESCE(l."MakePublic", FALSE)
                  )';
        ELSE
            EXECUTE '
                UPDATE "Linker_EntityToContacts" l
                SET "Scope" = 0
                FROM "Contacts" c
                LEFT JOIN "Addresses" a ON a."AddressID" = c."AddressID"
                LEFT JOIN "PhoneContacts" p ON p."PhoneContactID" = c."PhoneContactID"
                WHERE l."ContactID" = c."ContactID"
                  AND l."Scope" IS NULL
                  AND (
                      COALESCE(c."IsPrivate", FALSE)
                      OR COALESCE(a."IsPrivate", FALSE)
                      OR COALESCE(p."IsPrivate", FALSE)
                  )';
        END IF;
    ELSE
        IF EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'Linker_EntityToContacts'
              AND column_name = 'MakePublic') THEN
            EXECUTE '
                UPDATE "Linker_EntityToContacts"
                SET "Scope" = 0
                WHERE "Scope" IS NULL
                  AND NOT COALESCE("MakePublic", FALSE)';
        END IF;
    END IF;
END $$;

UPDATE "Linker_EntityToContacts"
SET "Scope" = 2
WHERE "Scope" IS NULL;

ALTER TABLE "Linker_EntityToContacts"
    ALTER COLUMN "Scope" SET DEFAULT 2;

ALTER TABLE "Linker_EntityToContacts"
    ALTER COLUMN "Scope" SET NOT NULL;

CREATE INDEX IF NOT EXISTS "IX_Linker_EntityToContacts_Scope"
    ON "Linker_EntityToContacts" ("Scope");

CREATE UNIQUE INDEX IF NOT EXISTS "UX_Linker_EntityToContacts_Artist_Primary"
    ON "Linker_EntityToContacts" ("ArtistID")
    WHERE "ArtistID" IS NOT NULL AND "Scope" = 1;

CREATE UNIQUE INDEX IF NOT EXISTS "UX_Linker_EntityToContacts_User_Primary"
    ON "Linker_EntityToContacts" ("UserID")
    WHERE "UserID" IS NOT NULL AND "Scope" = 1;

CREATE UNIQUE INDEX IF NOT EXISTS "UX_Linker_EntityToContacts_Venue_Primary"
    ON "Linker_EntityToContacts" ("VenueID")
    WHERE "VenueID" IS NOT NULL AND "Scope" = 1;

CREATE UNIQUE INDEX IF NOT EXISTS "UX_Linker_EntityToContacts_Vendor_Primary"
    ON "Linker_EntityToContacts" ("VendorID")
    WHERE "VendorID" IS NOT NULL AND "Scope" = 1;

ALTER TABLE "Linker_EntityToContacts"
    DROP COLUMN IF EXISTS "MakePublic";

ALTER TABLE "Contacts"
    DROP COLUMN IF EXISTS "IsPrivate";

ALTER TABLE "Addresses"
    DROP COLUMN IF EXISTS "IsPrivate";

ALTER TABLE "PhoneContacts"
    DROP COLUMN IF EXISTS "IsPrivate";

ALTER TABLE "Artists"
    DROP CONSTRAINT IF EXISTS "FK_Artists_Contacts_PrimaryContactID";

DROP INDEX IF EXISTS "IX_Artists_PrimaryContactID";

ALTER TABLE "Artists"
    DROP COLUMN IF EXISTS "PrimaryContactID";

ALTER TABLE "Users"
    DROP CONSTRAINT IF EXISTS "FK_Users_Contacts_PrimaryContactID";

DROP INDEX IF EXISTS "IX_Users_PrimaryContactID";

ALTER TABLE "Users"
    DROP COLUMN IF EXISTS "PrimaryContactID";

ALTER TABLE "Venues"
    DROP CONSTRAINT IF EXISTS "FK_Venues_Contacts_PrimaryContactID";

DROP INDEX IF EXISTS "IX_Venues_PrimaryContactID";

ALTER TABLE "Venues"
    DROP COLUMN IF EXISTS "PrimaryContactID";

ALTER TABLE "Vendors"
    DROP CONSTRAINT IF EXISTS "FK_Vendors_Contacts_PrimaryContactID";

DROP INDEX IF EXISTS "IX_Vendors_PrimaryContactID";

ALTER TABLE "Vendors"
    DROP COLUMN IF EXISTS "PrimaryContactID";

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_type t
        WHERE t.typname = 'ContactScope')
       AND NOT EXISTS (
           SELECT 1
           FROM pg_attribute a
           JOIN pg_class c ON c.oid = a.attrelid
           JOIN pg_type t ON t.oid = a.atttypid
           JOIN pg_namespace n ON n.oid = c.relnamespace
           WHERE a.attnum > 0
             AND NOT a.attisdropped
             AND n.nspname = 'public'
             AND t.typname = 'ContactScope') THEN
        DROP TYPE "ContactScope";
    END IF;
END $$;

COMMIT;


-- Run this script against the TAG database after backing up.
-- Purpose: update the managed-contact DynaForm to use scope instead of legacy privacy flags.

BEGIN;

INSERT INTO "Forms_Metadata"
("Forms_MetadataID", "Name", "APIURLpostfix", "H1", "H2", "H3", "FormBody", "FormStyle", "RequestType")
VALUES
    (901, 'ManagedContactForm', 'contact/manage', 'Managed Contact', 'Add or Update Managed Contact', 'Contact Scope', 'Use this form to add contact records for artists or users. Scope controls whether the link is private, primary, or secondary.', 'formstyle test', 'add/update')
ON CONFLICT ("Forms_MetadataID") DO UPDATE
SET
    "Name" = EXCLUDED."Name",
    "APIURLpostfix" = EXCLUDED."APIURLpostfix",
    "H1" = EXCLUDED."H1",
    "H2" = EXCLUDED."H2",
    "H3" = EXCLUDED."H3",
    "FormBody" = EXCLUDED."FormBody",
    "FormStyle" = EXCLUDED."FormStyle",
    "RequestType" = EXCLUDED."RequestType";

UPDATE "Forms_Metadata"
SET
    "FormBody" = 'Use this form to add contact records for artists or users. Scope controls whether the link is private, primary, or secondary.'
WHERE "Forms_MetadataID" = 901;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM "Forms_Metadata"
        WHERE "Forms_MetadataID" = 901) THEN
        DELETE FROM "Forms_Fields"
        WHERE "Forms_MetadataID" = 901
          AND lower("Name") = 'isprivate'
          AND "Forms_FieldID" <> 9012;

        INSERT INTO "Forms_Fields"
        ("Forms_FieldID", "Forms_MetadataID", "Name", "Type", "Label", "Placeholder", "DefaultValue", "FieldOrder", "Hidden", "IsRequired", "IsReadOnly", "ClassName", "Width", "Height", "Options")
        VALUES
            (9012, 901, 'scope', 'select', 'Scope', 'Choose private, primary, or secondary scope', 'secondary', 10, FALSE, FALSE, FALSE, 'select select-bordered w-full', '100%', NULL, '[{"value":"private","label":"Private"},{"value":"primary","label":"Primary"},{"value":"secondary","label":"Secondary"}]')
        ON CONFLICT ("Forms_FieldID") DO UPDATE
        SET
            "Forms_MetadataID" = EXCLUDED."Forms_MetadataID",
            "Name" = EXCLUDED."Name",
            "Type" = EXCLUDED."Type",
            "Label" = EXCLUDED."Label",
            "Placeholder" = EXCLUDED."Placeholder",
            "DefaultValue" = EXCLUDED."DefaultValue",
            "FieldOrder" = EXCLUDED."FieldOrder",
            "Hidden" = EXCLUDED."Hidden",
            "IsRequired" = EXCLUDED."IsRequired",
            "IsReadOnly" = EXCLUDED."IsReadOnly",
            "ClassName" = EXCLUDED."ClassName",
            "Width" = EXCLUDED."Width",
            "Height" = EXCLUDED."Height",
            "Options" = EXCLUDED."Options";
    ELSE
        RAISE NOTICE 'Forms_MetadataID 901 not found; skipping Forms_Fields scope upsert.';
    END IF;
END $$;

COMMIT;


-- Run this patch if the publish/moderation flags were not applied to all tables on first run.
-- Purpose: safe re-apply of IsPublished and IsModerationBlocked columns on all entity tables.
-- All statements use ADD COLUMN IF NOT EXISTS so re-running is harmless.

BEGIN;

ALTER TABLE "Listings"
    ADD COLUMN IF NOT EXISTS "IsPublished" boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS "IsModerationBlocked" boolean NOT NULL DEFAULT false;

ALTER TABLE "Artists"
    ADD COLUMN IF NOT EXISTS "IsPublished" boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS "IsModerationBlocked" boolean NOT NULL DEFAULT false;

ALTER TABLE "Users"
    ADD COLUMN IF NOT EXISTS "IsPublished" boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS "IsModerationBlocked" boolean NOT NULL DEFAULT false;

ALTER TABLE "Venues"
    ADD COLUMN IF NOT EXISTS "IsPublished" boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS "IsModerationBlocked" boolean NOT NULL DEFAULT false;

ALTER TABLE "Vendors"
    ADD COLUMN IF NOT EXISTS "IsPublished" boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS "IsModerationBlocked" boolean NOT NULL DEFAULT false;

COMMIT;
