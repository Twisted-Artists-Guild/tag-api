-- Add nullable JSON-encoded context color overrides to user preferences
-- Example value: {"user-primary":"#3B82F6","artist-1":"#14B8A6"}

ALTER TABLE "UserPreferences"
    ADD COLUMN "ContextColorOverrides" TEXT NULL;


-- Support posting comments as a selected profile context (e.g. an artist profile)
-- author_context_id: opaque frontend context key, e.g. "artist-12"
-- author_entity_type: entity kind for the selected identity, e.g. "artist" (NULL = base user)
-- author_entity_id: ID of the entity within author_entity_type (e.g. ArtistID)

ALTER TABLE public.comments
    ADD COLUMN author_context_id VARCHAR(100) NULL,
    ADD COLUMN author_entity_type VARCHAR(50) NULL,
    ADD COLUMN author_entity_id INTEGER NULL;

CREATE INDEX idx_comments_author_entity
    ON public.comments (author_entity_type, author_entity_id)
    WHERE author_entity_id IS NOT NULL;

COMMENT ON COLUMN public.comments.author_entity_type IS 'e.g. artist; NULL means the base user identity';
