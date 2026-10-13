-- Run this script as a database owner/superuser against the target TAG DB.
-- Purpose: seed a DynaForm definition for managing contacts through api/contact/manage.

BEGIN;

-- Metadata row for the new form.
INSERT INTO "Forms_Metadata"
    ("Forms_MetadataID", "Name", "APIURLpostfix", "H1", "H2", "H3", "FormBody", "FormStyle", "RequestType", "SegmentationType")
VALUES
    (901, 'ManageContactForm1', 'contact/manage', 'Manage Contact', 'Create Contact Record', 'Link contact to owner context', 'Use this form to add contact records for artists or users. Context and EntityID are hidden defaults supplied by portal pages.', 'formstyle test', 'add', 'portal_contact')
ON CONFLICT ("Forms_MetadataID") DO UPDATE
SET
    "Name" = EXCLUDED."Name",
    "APIURLpostfix" = EXCLUDED."APIURLpostfix",
    "H1" = EXCLUDED."H1",
    "H2" = EXCLUDED."H2",
    "H3" = EXCLUDED."H3",
    "FormBody" = EXCLUDED."FormBody",
    "FormStyle" = EXCLUDED."FormStyle",
    "RequestType" = EXCLUDED."RequestType",
    "SegmentationType" = EXCLUDED."SegmentationType";

-- Remove existing field seed for this form id so reruns stay deterministic.
DELETE FROM "Forms_Fields" WHERE "Forms_MetadataID" = 901;

INSERT INTO "Forms_Fields"
("Forms_FieldID", "Forms_MetadataID", "Name", "Type", "Label", "Placeholder", "DefaultValue", "FieldOrder", "Hidden", "IsRequired", "IsReadOnly", "ClassName", "Width", "Height", "Options")
VALUES
    (9001, 901, 'context', 'text', 'Context', 'artist/user/vendor/venue', 'artist', 1, TRUE, TRUE, FALSE, 'input input-bordered w-full', '100%', NULL, NULL),
    (9002, 901, 'entityID', 'number', 'Entity ID', 'Owner entity ID', '0', 2, TRUE, TRUE, FALSE, 'input input-bordered w-full', '100%', NULL, NULL),
    (9003, 901, 'contactType', 'select', 'Contact Type', 'Select contact type', 'url', 3, FALSE, TRUE, FALSE, 'select select-bordered w-full', '100%', NULL, '[{"value":"email","label":"Email"},{"value":"url","label":"URL"},{"value":"phone","label":"Phone"},{"value":"address","label":"Address"}]'),
    (9004, 901, 'label', 'text', 'Label', 'Business Email, Booking Link, Studio Phone', '', 4, FALSE, FALSE, FALSE, 'input input-bordered w-full', '100%', NULL, NULL),
    (9005, 901, 'category', 'text', 'Category', 'booking, studio, support, portfolio', '', 5, FALSE, FALSE, FALSE, 'input input-bordered w-full', '100%', NULL, NULL),
    (9006, 901, 'value', 'text', 'Value', 'Email address, URL, or fallback phone value', '', 6, FALSE, FALSE, FALSE, 'input input-bordered w-full', '100%', NULL, NULL),
    (9007, 901, 'handle', 'text', 'Handle', '@username or shorthand', '', 7, FALSE, FALSE, FALSE, 'input input-bordered w-full', '100%', NULL, NULL),
    (9008, 901, 'description', 'textarea', 'Description', 'Optional details/purpose for this contact', '', 8, FALSE, FALSE, FALSE, 'textarea textarea-bordered w-full', '100%', '3', NULL),
    (9011, 901, 'displayOrder', 'number', 'Display Order', '0', '0', 9, FALSE, FALSE, FALSE, 'input input-bordered w-full', '100%', NULL, NULL),
    (9012, 901, 'isPrivate', 'checkbox', 'Is Private', '', 'false', 10, FALSE, FALSE, FALSE, 'checkbox', '100%', NULL, NULL),
    (9013, 901, 'phoneNumber', 'text', 'Phone Number', 'Only used when contact type is phone', '', 11, FALSE, FALSE, FALSE, 'input input-bordered w-full', '100%', NULL, NULL),
    (9014, 901, 'phoneDescription', 'text', 'Phone Description', 'Office line, text only, etc', '', 12, FALSE, FALSE, FALSE, 'input input-bordered w-full', '100%', NULL, NULL),
    (9015, 901, 'phoneContactLabelID', 'select', 'Phone Label', 'Select phone label', '2', 13, FALSE, FALSE, FALSE, 'select select-bordered w-full', '100%', NULL, '[{"value":"1","label":"home"},{"value":"2","label":"office"},{"value":"3","label":"mobile"},{"value":"4","label":"other"}]'),
    (9016, 901, 'addressLine1', 'text', 'Address Line 1', 'Only used when contact type is address', '', 14, FALSE, FALSE, FALSE, 'input input-bordered w-full', '100%', NULL, NULL),
    (9017, 901, 'addressLine2', 'text', 'Address Line 2', '', '', 15, FALSE, FALSE, FALSE, 'input input-bordered w-full', '100%', NULL, NULL),
    (9018, 901, 'city', 'text', 'City', '', '', 16, FALSE, FALSE, FALSE, 'input input-bordered w-full', '100%', NULL, NULL),
    (9019, 901, 'state', 'text', 'State', '', '', 17, FALSE, FALSE, FALSE, 'input input-bordered w-full', '100%', NULL, NULL),
    (9020, 901, 'region', 'text', 'Region', '', '', 18, FALSE, FALSE, FALSE, 'input input-bordered w-full', '100%', NULL, NULL),
    (9021, 901, 'zipCode', 'text', 'Postal Code', '', '', 19, FALSE, FALSE, FALSE, 'input input-bordered w-full', '100%', NULL, NULL),
    (9022, 901, 'country', 'text', 'Country', '', '', 20, FALSE, TRUE, FALSE, 'input input-bordered w-full', '100%', NULL, NULL),
    (9023, 901, 'operationHours', 'text', 'Operation Hours', '', '', 21, FALSE, FALSE, FALSE, 'input input-bordered w-full', '100%', NULL, NULL);

COMMIT;
