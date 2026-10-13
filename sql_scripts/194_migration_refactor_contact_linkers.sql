-- ============================================================================
-- Migration Script (PostgreSQL): Refactor Linker_EntityToContacts & Drop Linker_UserAndArtistToContacts
-- ============================================================================

BEGIN;

-- ============================================================================
-- PART 1: Refactor Linker_EntityToContacts
-- ============================================================================

-- Step 1: Rename the existing table
ALTER TABLE "Linker_EntityToContacts" RENAME TO "Linker_EntityToContacts_OLD";

-- Rename indexes/constraints so they don't conflict
ALTER INDEX "Linker_EntityToContacts_pkey" RENAME TO "Linker_EntityToContacts_pkey_OLD";
ALTER INDEX "IX_Linker_EntityToContacts_ContactID" RENAME TO "IX_Linker_EntityToContacts_ContactID_OLD";

-- Step 2: Create the new table with polymorphic structure
CREATE TABLE "Linker_EntityToContacts" (
    "Linker_EntityToContactID" SERIAL PRIMARY KEY,
    "ContactID"                INT          NOT NULL,
    "EntityType"               VARCHAR(50)  NOT NULL,
    "EntityID"                 INT          NOT NULL,
    "Scope"                    INT          NOT NULL DEFAULT 2,
    "DisplayOrder"             INT          NOT NULL DEFAULT 0,
    CONSTRAINT "FK_Linker_EntityToContacts_Contacts_ContactID"
        FOREIGN KEY ("ContactID") REFERENCES "Contacts"("ContactID") ON DELETE CASCADE
);

-- Step 3: Create indexes
CREATE UNIQUE INDEX "IX_Linker_EntityToContacts_EntityType_EntityID_ContactID"
    ON "Linker_EntityToContacts" ("EntityType", "EntityID", "ContactID");

CREATE INDEX "IX_Linker_EntityToContacts_EntityType_EntityID"
    ON "Linker_EntityToContacts" ("EntityType", "EntityID");

CREATE INDEX "IX_Linker_EntityToContacts_ContactID"
    ON "Linker_EntityToContacts" ("ContactID");

-- Step 4: Migrate data from old table to new table (preserve IDs)
INSERT INTO "Linker_EntityToContacts" (
    "Linker_EntityToContactID",
    "ContactID",
    "EntityType",
    "EntityID",
    "Scope",
    "DisplayOrder"
)
SELECT
    old."Linker_EntityToContactID",
    old."ContactID",
    CASE
        WHEN old."UserID"   IS NOT NULL THEN 'User'
        WHEN old."ArtistID" IS NOT NULL THEN 'Artist'
        WHEN old."VenueID"  IS NOT NULL THEN 'Venue'
        WHEN old."VendorID" IS NOT NULL THEN 'Vendor'
    END,
    COALESCE(old."UserID", old."ArtistID", old."VenueID", old."VendorID"),
    old."Scope",
    old."DisplayOrder"
FROM "Linker_EntityToContacts_OLD" old
WHERE COALESCE(old."UserID", old."ArtistID", old."VenueID", old."VendorID") IS NOT NULL;

-- Reset the sequence to continue after the max migrated ID
SELECT setval(
    pg_get_serial_sequence('"Linker_EntityToContacts"', 'Linker_EntityToContactID'),
    COALESCE((SELECT MAX("Linker_EntityToContactID") FROM "Linker_EntityToContacts"), 0)
);

-- ============================================================================
-- PART 2: Migrate Linker_UserAndArtistToContacts into Linker_EntityToContacts
-- ============================================================================

-- Step 5a: Create Contact records for Address-based links (if not already existing)
INSERT INTO "Contacts" ("ContactType", "Label", "AddressID", "Description")
SELECT
    'Address',
    l."Label",
    l."AddressID",
    'Migrated from Linker_UserAndArtistToContacts'
FROM "Linker_UserAndArtistToContacts" l
WHERE l."AddressID" IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM "Contacts" c WHERE c."AddressID" = l."AddressID"
  );

-- Step 5b: Create Contact records for PhoneContact-based links
INSERT INTO "Contacts" ("ContactType", "Label", "PhoneContactID", "Description")
SELECT
    'Phone',
    l."Label",
    l."PhoneContactID",
    'Migrated from Linker_UserAndArtistToContacts'
FROM "Linker_UserAndArtistToContacts" l
WHERE l."PhoneContactID" IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM "Contacts" c WHERE c."PhoneContactID" = l."PhoneContactID"
  );

-- Step 5c: Create Contact records for ExternalLink-based links
INSERT INTO "Contacts" ("ContactType", "Label", "Value", "Handle", "Description")
SELECT
    'ExternalLink',
    l."Label",
    el."URL",
    el."Handle",
    COALESCE(el."Description", 'Migrated from Linker_UserAndArtistToContacts')
FROM "Linker_UserAndArtistToContacts" l
INNER JOIN "ExternalLinks" el ON el."ExternalLinkID" = l."ExternalLinkID"
WHERE l."ExternalLinkID" IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM "Contacts" c WHERE c."Value" = el."URL" AND c."ContactType" = 'ExternalLink'
  );

-- Step 6: Insert entity-to-contact links for Artist-owned rows
-- Artist + Address
INSERT INTO "Linker_EntityToContacts" ("ContactID", "EntityType", "EntityID", "Scope", "DisplayOrder")
SELECT c."ContactID", 'Artist', l."ArtistID",
       CASE WHEN l."MakePublic" = TRUE THEN 1 ELSE 0 END,
       0
FROM "Linker_UserAndArtistToContacts" l
INNER JOIN "Contacts" c ON c."AddressID" = l."AddressID"
WHERE l."ArtistID" IS NOT NULL AND l."AddressID" IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM "Linker_EntityToContacts" ec
      WHERE ec."EntityType" = 'Artist' AND ec."EntityID" = l."ArtistID" AND ec."ContactID" = c."ContactID"
  );

-- Artist + PhoneContact
INSERT INTO "Linker_EntityToContacts" ("ContactID", "EntityType", "EntityID", "Scope", "DisplayOrder")
SELECT c."ContactID", 'Artist', l."ArtistID",
       CASE WHEN l."MakePublic" = TRUE THEN 1 ELSE 0 END,
       0
FROM "Linker_UserAndArtistToContacts" l
INNER JOIN "Contacts" c ON c."PhoneContactID" = l."PhoneContactID"
WHERE l."ArtistID" IS NOT NULL AND l."PhoneContactID" IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM "Linker_EntityToContacts" ec
      WHERE ec."EntityType" = 'Artist' AND ec."EntityID" = l."ArtistID" AND ec."ContactID" = c."ContactID"
  );

-- Artist + ExternalLink
INSERT INTO "Linker_EntityToContacts" ("ContactID", "EntityType", "EntityID", "Scope", "DisplayOrder")
SELECT c."ContactID", 'Artist', l."ArtistID",
       CASE WHEN l."MakePublic" = TRUE THEN 1 ELSE 0 END,
       0
FROM "Linker_UserAndArtistToContacts" l
INNER JOIN "ExternalLinks" el ON el."ExternalLinkID" = l."ExternalLinkID"
INNER JOIN "Contacts" c ON c."Value" = el."URL" AND c."ContactType" = 'ExternalLink'
WHERE l."ArtistID" IS NOT NULL AND l."ExternalLinkID" IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM "Linker_EntityToContacts" ec
      WHERE ec."EntityType" = 'Artist' AND ec."EntityID" = l."ArtistID" AND ec."ContactID" = c."ContactID"
  );

-- Step 7: Insert entity-to-contact links for User-owned rows
-- User + Address
INSERT INTO "Linker_EntityToContacts" ("ContactID", "EntityType", "EntityID", "Scope", "DisplayOrder")
SELECT c."ContactID", 'User', l."UserID",
       CASE WHEN l."MakePublic" = TRUE THEN 1 ELSE 0 END,
       0
FROM "Linker_UserAndArtistToContacts" l
INNER JOIN "Contacts" c ON c."AddressID" = l."AddressID"
WHERE l."UserID" IS NOT NULL AND l."AddressID" IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM "Linker_EntityToContacts" ec
      WHERE ec."EntityType" = 'User' AND ec."EntityID" = l."UserID" AND ec."ContactID" = c."ContactID"
  );

-- User + PhoneContact
INSERT INTO "Linker_EntityToContacts" ("ContactID", "EntityType", "EntityID", "Scope", "DisplayOrder")
SELECT c."ContactID", 'User', l."UserID",
       CASE WHEN l."MakePublic" = TRUE THEN 1 ELSE 0 END,
       0
FROM "Linker_UserAndArtistToContacts" l
INNER JOIN "Contacts" c ON c."PhoneContactID" = l."PhoneContactID"
WHERE l."UserID" IS NOT NULL AND l."PhoneContactID" IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM "Linker_EntityToContacts" ec
      WHERE ec."EntityType" = 'User' AND ec."EntityID" = l."UserID" AND ec."ContactID" = c."ContactID"
  );

-- User + ExternalLink
INSERT INTO "Linker_EntityToContacts" ("ContactID", "EntityType", "EntityID", "Scope", "DisplayOrder")
SELECT c."ContactID", 'User', l."UserID",
       CASE WHEN l."MakePublic" = TRUE THEN 1 ELSE 0 END,
       0
FROM "Linker_UserAndArtistToContacts" l
INNER JOIN "ExternalLinks" el ON el."ExternalLinkID" = l."ExternalLinkID"
INNER JOIN "Contacts" c ON c."Value" = el."URL" AND c."ContactType" = 'ExternalLink'
WHERE l."UserID" IS NOT NULL AND l."ExternalLinkID" IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM "Linker_EntityToContacts" ec
      WHERE ec."EntityType" = 'User' AND ec."EntityID" = l."UserID" AND ec."ContactID" = c."ContactID"
  );

-- ============================================================================
-- PART 3: Verification & Cleanup
-- ============================================================================

-- Step 8: Verify migration counts
DO $$
DECLARE
    old_count INT;
    legacy_count INT;
    new_count INT;
BEGIN
    SELECT COUNT(*) INTO old_count FROM "Linker_EntityToContacts_OLD";
    SELECT COUNT(*) INTO legacy_count FROM "Linker_UserAndArtistToContacts";
    SELECT COUNT(*) INTO new_count FROM "Linker_EntityToContacts";

    RAISE NOTICE '=== Migration Verification ===';
    RAISE NOTICE 'Rows in Linker_EntityToContacts_OLD: %', old_count;
    RAISE NOTICE 'Rows in Linker_UserAndArtistToContacts: %', legacy_count;
    RAISE NOTICE 'Rows in new Linker_EntityToContacts: %', new_count;
    RAISE NOTICE 'Expected minimum from old table: %', old_count;

    IF new_count < old_count THEN
        RAISE EXCEPTION 'Migration validation failed: new table has fewer rows (%) than old table (%)', new_count, old_count;
    END IF;

    RAISE NOTICE 'Migration validation passed.';
END $$;

-- Step 9: Drop old tables (uncomment when confident after verifying data)
-- DROP TABLE "Linker_EntityToContacts_OLD";
-- DROP TABLE "Linker_UserAndArtistToContacts";

COMMIT;