BEGIN;

-- 1. Create conversations table
CREATE TABLE IF NOT EXISTS public.conversations (
    id serial NOT NULL,
    title varchar(250) NULL,
    is_group boolean NOT NULL DEFAULT false,
    created_at timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    last_message_at timestamp with time zone NULL,

    CONSTRAINT pk_conversations PRIMARY KEY (id)
);

-- 2. Create conversation_participants table
CREATE TABLE IF NOT EXISTS public.conversation_participants (
    id serial NOT NULL,
    conversation_id integer NOT NULL,
    user_id integer NOT NULL,
    joined_at timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    last_read_at timestamp with time zone NULL,

    CONSTRAINT pk_conversation_participants PRIMARY KEY (id),
    CONSTRAINT fk_conversation_participants_conversation FOREIGN KEY (conversation_id) 
        REFERENCES public.conversations (id) ON DELETE CASCADE
);

-- 3. Create messages table
CREATE TABLE IF NOT EXISTS public.messages (
    id serial NOT NULL,
    encrypted_body text NULL,
    is_encrypted boolean NOT NULL DEFAULT true,
    is_edited boolean NOT NULL DEFAULT false,
    from_user_id integer NOT NULL,
    picture_id integer NULL,
    sent_at timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    conversation_id integer NULL,
    to_user_id integer NULL,
    is_deleted boolean NOT NULL DEFAULT false,
    is_read boolean NOT NULL DEFAULT false,

    CONSTRAINT pk_messages PRIMARY KEY (id),
    CONSTRAINT fk_messages_conversation FOREIGN KEY (conversation_id) 
        REFERENCES public.conversations (id) ON DELETE SET NULL
);

-- 4. Create message_attachments table
CREATE TABLE IF NOT EXISTS public.message_attachments (
    id serial NOT NULL,
    message_id integer NOT NULL,
    file_name text NOT NULL,
    content_type text NOT NULL,
    url text NOT NULL,
    size bigint NOT NULL, -- long maps to bigint
    created_at timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),

    CONSTRAINT pk_message_attachments PRIMARY KEY (id),
    CONSTRAINT fk_message_attachments_message FOREIGN KEY (message_id) 
        REFERENCES public.messages (id) ON DELETE CASCADE
);

-- 5. Create message_impressions (Reactions) table
CREATE TABLE IF NOT EXISTS public.message_impressions (
    id serial NOT NULL,
    message_id integer NOT NULL,
    impression_id integer NOT NULL,
    user_id integer NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),

    CONSTRAINT pk_message_impressions PRIMARY KEY (id),
    CONSTRAINT fk_message_impressions_message FOREIGN KEY (message_id) 
        REFERENCES public.messages (id) ON DELETE CASCADE
);

--- 
--- High-Capacity Performance Indexes
--- 

-- Optimization for participant lists and fast message inbox badge counts
CREATE INDEX IF NOT EXISTS idx_conversation_participants_lookup 
ON public.conversation_participants (user_id, conversation_id);

-- Speed optimization for loading sequential history loops within threads
CREATE INDEX IF NOT EXISTS idx_messages_conversation_sent 
ON public.messages (conversation_id, sent_at DESC) 
WHERE conversation_id IS NOT NULL;

-- Optimization for indexing standard 1-to-1 legacy direct queries
CREATE INDEX IF NOT EXISTS idx_messages_direct_lookup 
ON public.messages (from_user_id, to_user_id) 
WHERE conversation_id IS NULL;

-- Optimization for processing attachments and media galleries per chat thread
CREATE INDEX IF NOT EXISTS idx_message_attachments_msg 
ON public.message_attachments (message_id);

-- Composite Unique index ensuring a user can log only one of the exact same impression per message
CREATE UNIQUE INDEX IF NOT EXISTS idx_message_impressions_unique_user_reaction
ON public.message_impressions (message_id, user_id, impression_id);

COMMIT;