-- Run this script as a database owner or superuser.
-- It transfers ownership of all existing public tables to web_connection,
-- grants runtime access to testing_web_connection and mbanga,
-- and sets default privileges for future tables and sequences created by web_connection.
--
-- Updated to include all contact model tables from 116_contact_refactor.sql migration:
-- - PhoneContactLabels, ContactLabels, Contacts, Linker_EntityToContacts, LinkCategories
-- - Plus existing: Addresses, PhoneContacts, ExternalLinks, and all entity tables

BEGIN;

-- Ensure all application roles can resolve objects in the public schema.
GRANT USAGE ON SCHEMA public TO web_connection, testing_web_connection, mbanga;

-- Transfer ownership of all existing public tables to web_connection.
DO $$
DECLARE
    table_record record;
BEGIN
    FOR table_record IN
        SELECT schemaname, tablename
        FROM pg_tables
        WHERE schemaname = 'public'
    LOOP
        EXECUTE format(
            'ALTER TABLE %I.%I OWNER TO web_connection;',
            table_record.schemaname,
            table_record.tablename);
    END LOOP;
END $$;

-- Explicit permissions for all contact-related tables (from 116_contact_refactor.sql migration).
-- These ensure tables created by the migration script have proper access control.
DO $$
BEGIN
    -- New unified contact model tables
    EXECUTE 'GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER ON "PhoneContactLabels" TO testing_web_connection, mbanga' 
        WHERE EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name = 'PhoneContactLabels');
    EXECUTE 'GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER ON "ContactLabels" TO testing_web_connection, mbanga'
        WHERE EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name = 'ContactLabels');
    EXECUTE 'GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER ON "Contacts" TO testing_web_connection, mbanga'
        WHERE EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name = 'Contacts');
    EXECUTE 'GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER ON "Linker_EntityToContacts" TO testing_web_connection, mbanga'
        WHERE EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name = 'Linker_EntityToContacts');
    EXECUTE 'GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER ON "LinkCategories" TO testing_web_connection, mbanga'
        WHERE EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name = 'LinkCategories');
    
    -- Existing contact-related tables
    EXECUTE 'GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER ON "Addresses" TO testing_web_connection, mbanga'
        WHERE EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name = 'Addresses');
    EXECUTE 'GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER ON "PhoneContacts" TO testing_web_connection, mbanga'
        WHERE EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name = 'PhoneContacts');
    EXECUTE 'GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER ON "ExternalLinks" TO testing_web_connection, mbanga'
        WHERE EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name = 'ExternalLinks');
    
    -- Entity tables that reference contacts
    EXECUTE 'GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER ON "Artists" TO testing_web_connection, mbanga'
        WHERE EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name = 'Artists');
    EXECUTE 'GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER ON "Users" TO testing_web_connection, mbanga'
        WHERE EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name = 'Users');
    EXECUTE 'GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER ON "Venues" TO testing_web_connection, mbanga'
        WHERE EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name = 'Venues');
    EXECUTE 'GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER ON "Vendors" TO testing_web_connection, mbanga'
        WHERE EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name = 'Vendors');
END $$;

-- Blanket permissions on all tables (catches any missed tables).
GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER
ON ALL TABLES IN SCHEMA public
TO testing_web_connection, mbanga;

-- Existing sequence permissions for identity/serial-backed inserts.
GRANT USAGE, SELECT
ON ALL SEQUENCES IN SCHEMA public
TO testing_web_connection, mbanga;

-- Future tables created by web_connection should automatically grant runtime access.
ALTER DEFAULT PRIVILEGES FOR ROLE web_connection IN SCHEMA public
GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER ON TABLES
TO testing_web_connection, mbanga;

-- Future sequences created by web_connection should also be usable.
ALTER DEFAULT PRIVILEGES FOR ROLE web_connection IN SCHEMA public
GRANT USAGE, SELECT ON SEQUENCES
TO testing_web_connection, mbanga;

COMMIT;
