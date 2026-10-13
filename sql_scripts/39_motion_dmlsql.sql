BEGIN;

-- 1. Seed the Metadata for the Contest Form
INSERT INTO public."Forms_Metadata" (
    "Forms_MetadataID", 
    "Name", 
    "H1", 
    "H2", 
    "RequestType", 
    "APIURLpostfix"
) VALUES (
    12, 
    'CreateMotionForm', 
    'Propose New Motion', 
    'Fill in the details to propose new motion', 
    'POST', 
    'api/motion/create'
);

-- 2. Seed the Fields for the Contest Form
INSERT INTO public."Forms_Fields" (
    "Forms_FieldID", 
    "Forms_MetadataID", 
    "Name", 
    "Label", 
    "Type", 
    "IsRequired", 
    "FieldOrder", 
    "Placeholder", 
    "RegexValidationPattern",
    "ClassName",
    "Width",
	"Options"

) VALUES 
-- Row 1: Title and Slug
(730, 12, 'Title', 'Motion Title', 'tiptap_title', true, 1, 'Enter rich text title...', NULL, 'styles.input', '100%',NULL),
(731, 12, 'Slug', 'URL Slug', 'text', true, 2, '', '^[a-z0-9-]+$', 'input-dyna', '100%',NULL),

-- Row 2: Prompt
(732, 12, 'Subject', 'Subject', 'text', true, 3, '', NULL, 'input-dyna', '100%',NULL),

-- Row 3: Rich Text Areas
(733, 12, 'Description', 'Detailed Description', 'tiptap_portfolio', false, 4, '', NULL, 'styles.input', '100%',NULL),
(734, 12, 'Duration', 'Duration', 'select', true, 5, 'Select duration...', NULL, 'form-select', '50%','[{"label": "Weekly", "value": "Weekly"}, {"label": "Monthly", "value": "Monthly"}, {"label": "Quarterly", "value": "Quarterly"}, {"label": "Annual", "value": "Annual"}]'
),

-- Row 6: Visuals
(735, 12, 'Attachment', '', 'url', false, 6, 'https://...', NULL, 'input-dyna', '100%',NULL),
(736, 12, 'Status', 'Initial Status', 'select', true, 7, 'Select status...', NULL, 'form-select', '50%','[{"label": "Draft", "value": "Draft"}, {"label": "Proposed", "value": "Proposed"}, {"label": "Archived", "value": "Archived"}]'
);



INSERT INTO public.permissions (name) VALUES 
('motion:create'),
('motion:update'),
('motion:delete'),
('motion:view'),
('motion:second'),
('motion:vote');

INSERT INTO public.role_permissions (role_id,permission_id) VALUES 
(4,26),
(4,27),
(4,28),
(4,29),
(4,30),
(4,31);

COMMIT;
