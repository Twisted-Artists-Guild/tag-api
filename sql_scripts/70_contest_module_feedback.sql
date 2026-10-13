-- Rename the table
ALTER TABLE public.master_impressions RENAME TO primary_impressions;

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
    "Width"
) VALUES 
-- Row 4: Scheduling (Dates)
(725, 11, 'WarmupEndDate', 'Warmup End Date', 'datetime-local', true, 8, NULL, NULL, 'styles.input', '50%');

UPDATE public."Forms_Fields" 
SET "FieldOrder" = '9'
WHERE "Forms_FieldID" = '721';

UPDATE public."Forms_Fields" 
SET "FieldOrder" = '7'
WHERE "Forms_FieldID" = '722';

UPDATE public."Forms_Fields" 
SET "FieldOrder" = '10'
WHERE "Forms_FieldID" = '723';

UPDATE public."Forms_Fields" 
SET "FieldOrder" = '11'
WHERE "Forms_FieldID" = '724';

-- Add the missing column to your PostgreSQL table
ALTER TABLE public."contests" 
ADD COLUMN "warmup_end_date" TIMESTAMP NULL;