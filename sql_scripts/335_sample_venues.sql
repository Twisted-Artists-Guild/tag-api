-- Story #335: sample venues for trying out the public venue directory (/venues).
-- Adds 16 published sample venues across 9 states, plus one unpublished and one blocked venue that the directory must
-- NOT show. Every name starts with "Sample " and every description says it is not a real business.
-- Idempotent: a venue whose name already exists (ignoring case) is skipped, and tags are reused.
--
-- Run against the right database first and check which server you are on:
--   SELECT current_database(), inet_server_addr();
--
-- Needs the categories (325), tags (326) and regions (327) scripts to have been run first.
-- Categories and regions are looked up by slug; a link is skipped if that category or region is missing or inactive.
-- To take these venues out again, run 335_sample_venues_remove.sql.

BEGIN;

DO $$
DECLARE
    r record;
    v_address_id integer;
    v_venue_id integer;
    v_added integer := 0;
    v_skipped integer := 0;
BEGIN
    FOR r IN
        SELECT * FROM (VALUES
            ('Sample Harbor Lights Theater', 'A restored theater near the river with a proscenium stage.',
                '101 Bay Street', 'Savannah', 'GA', '31401', true, false,
                ARRAY['theaters'], ARRAY['stage', 'parking', 'wheelchair accessible', 'sound system'], ARRAY['georgia']),
            ('Sample Peachtree Gallery', 'A bright white-wall gallery for monthly openings.',
                '202 Peachtree Place NE', 'Atlanta', 'GA', '30308', true, false,
                ARRAY['galleries'], ARRAY['wifi', 'wheelchair accessible'], ARRAY['georgia', 'atlanta-metro']),
            ('Sample Midtown Music Club', 'A standing-room club with a full bar and a house sound system.',
                '303 Juniper Street NE', 'Atlanta', 'GA', '30309', true, false,
                ARRAY['clubs', 'bars'], ARRAY['stage', 'bar', 'sound system', 'green room'], ARRAY['georgia', 'atlanta-metro']),
            ('Sample Augusta Riverside Hall', 'A hall with a lawn for outdoor markets and receptions.',
                '404 Riverwalk Drive', 'Augusta', 'GA', '30901', true, false,
                ARRAY['other-event-spaces'], ARRAY['outdoor', 'parking', 'kitchen'], ARRAY['georgia']),
            ('Sample Queen City Studio', 'A shared studio with workshop tables and a projector.',
                '505 Trade Street', 'Charlotte', 'NC', '28202', true, false,
                ARRAY['studios'], ARRAY['wifi', 'projector', 'wheelchair accessible'], ARRAY['north-carolina']),
            ('Sample Blue Ridge Convention Center', 'A mountain-town convention center with loading docks and breakout rooms.',
                '606 Haywood Road', 'Asheville', 'NC', '28801', true, false,
                ARRAY['convention-centers'], ARRAY['parking', 'loading dock', 'wifi', 'kitchen', 'wheelchair accessible'], ARRAY['north-carolina']),
            ('Sample Music Row Listening Room', 'A small listening room for acoustic and songwriter nights.',
                '707 Demonbreun Street', 'Nashville', 'TN', '37203', true, false,
                ARRAY['clubs', 'theaters'], ARRAY['stage', 'sound system', 'bar', 'green room'], ARRAY['tennessee']),
            ('Sample Riverfront Art Space', 'A warehouse space used for galleries and open studios.',
                '808 Front Street', 'Memphis', 'TN', '38103', true, false,
                ARRAY['galleries', 'studios'], ARRAY['wifi', 'all ages', 'parking'], ARRAY['tennessee']),
            ('Sample Orange Grove Theater', 'A family-friendly theater with a green room and ample parking.',
                '909 Orange Avenue', 'Orlando', 'FL', '32801', true, false,
                ARRAY['theaters'], ARRAY['stage', 'parking', 'wheelchair accessible', 'green room'], ARRAY['florida']),
            ('Sample Biscayne Bar and Stage', 'A bayfront bar with a small stage and a late-night schedule.',
                '1010 Biscayne Boulevard', 'Miami', 'FL', '33131', true, false,
                ARRAY['bars', 'clubs'], ARRAY['bar', 'stage', 'sound system'], ARRAY['florida']),
            ('Sample Congress Avenue Gallery', 'A downtown gallery open to all ages.',
                '1111 Congress Avenue', 'Austin', 'TX', '78701', true, false,
                ARRAY['galleries'], ARRAY['wifi', 'wheelchair accessible', 'all ages'], ARRAY['texas']),
            ('Sample Lone Star Convention Hall', 'A large hall with a commercial kitchen and on-site parking.',
                '1212 Commerce Street', 'Dallas', 'TX', '75201', true, false,
                ARRAY['convention-centers', 'other-event-spaces'], ARRAY['parking', 'loading dock', 'kitchen', 'projector'], ARRAY['texas']),
            ('Sample Mission District Studio', 'A sunny studio for classes, rehearsals and small shows.',
                '1313 Valencia Street', 'San Francisco', 'CA', '94110', true, false,
                ARRAY['studios'], ARRAY['wifi', 'projector', 'all ages'], ARRAY['california']),
            ('Sample Hudson Yards Loft', 'A loft that works as a gallery or a pop-up venue.',
                '1414 West 34th Street', 'New York', 'NY', '10001', true, false,
                ARRAY['other-event-spaces', 'galleries'], ARRAY['wheelchair accessible', 'wifi', 'projector'], ARRAY['new-york']),
            ('Sample Lakefront Pavilion', 'An open-air pavilion on the lake for summer events.',
                '1515 Lake Shore Drive', 'Chicago', 'IL', '60611', true, false,
                ARRAY['other-event-spaces'], ARRAY['outdoor', 'stage', 'parking'], ARRAY['illinois']),
            ('Sample Rainier Club House', 'A club with a stage, a bar and an all-ages matinee.',
                '1616 Pike Street', 'Seattle', 'WA', '98101', true, false,
                ARRAY['clubs'], ARRAY['stage', 'bar', 'sound system', 'all ages'], ARRAY['washington']),
            -- Hidden from the directory on purpose: one is not published, one is blocked by moderation.
            ('Sample Unpublished Venue', 'Not published yet, so it must not appear in the directory.',
                '1717 Draft Lane', 'Atlanta', 'GA', '30303', false, false,
                ARRAY['theaters'], ARRAY['stage'], ARRAY['georgia']),
            ('Sample Blocked Venue', 'Blocked by moderation, so it must not appear in the directory.',
                '1818 Blocked Court', 'Atlanta', 'GA', '30303', true, true,
                ARRAY['theaters'], ARRAY['stage'], ARRAY['georgia'])
        ) AS s(name, summary, address_line1, city, state, zip_code, is_published, is_blocked, categories, tags, regions)
    LOOP
        IF EXISTS (SELECT 1 FROM public."Venues" WHERE lower("Name") = lower(r.name)) THEN
            v_skipped := v_skipped + 1;
            CONTINUE;
        END IF;

        INSERT INTO public."Addresses" ("AddressLine1", "City", "State", "ZipCode", "Country")
        VALUES (r.address_line1, r.city, r.state, r.zip_code, 'USA')
        RETURNING "AddressID" INTO v_address_id;

        -- Name_Plaintext and Description_Plaintext are filled in by the tr_venues_set_plaintext trigger.
        INSERT INTO public."Venues" ("Name", "AddressID", "Description", "IsPublished", "IsModerationBlocked")
        VALUES (r.name, v_address_id, '<p>' || r.summary || ' Sample venue for testing; not a real business.</p>', r.is_published, r.is_blocked)
        RETURNING "VenueID" INTO v_venue_id;

        INSERT INTO public."Linker_VenueToCategory" ("VenueID", "VenueCategoryID")
        SELECT v_venue_id, c."VenueCategoryID"
        FROM public."VenueCategories" c
        WHERE c."Slug" = ANY (r.categories) AND c."IsActive";

        INSERT INTO public."VenueTags" ("Name")
        SELECT t FROM unnest(r.tags) AS t
        ON CONFLICT ("Name") DO NOTHING;

        INSERT INTO public."Linker_VenueToTag" ("VenueID", "VenueTagID")
        SELECT v_venue_id, vt."VenueTagID"
        FROM public."VenueTags" vt
        WHERE vt."Name" = ANY (r.tags);

        INSERT INTO public."Linker_VenueToRegion" ("VenueID", "RegionID")
        SELECT v_venue_id, g."RegionID"
        FROM public."Regions" g
        WHERE g."Slug" = ANY (r.regions) AND g."IsActive";

        v_added := v_added + 1;
    END LOOP;

    RAISE NOTICE 'Sample venues added: %, already present and skipped: %', v_added, v_skipped;
END
$$;

COMMIT;

-- Check: published sample venues by state (expect 16 in total on a first run).
SELECT a."State", count(*) AS published_sample_venues
FROM public."Venues" v
JOIN public."Addresses" a ON a."AddressID" = v."AddressID"
WHERE v."Name" LIKE 'Sample %' AND v."IsPublished" AND NOT v."IsModerationBlocked"
GROUP BY a."State"
ORDER BY a."State";
