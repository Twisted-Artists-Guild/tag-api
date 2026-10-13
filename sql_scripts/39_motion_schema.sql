BEGIN;

-- 1. Create motions table (Updated with slug)
CREATE TABLE IF NOT EXISTS public.motions (
    id serial NOT NULL,
    title text NOT NULL,
    slug varchar(255) NOT NULL, -- Added unique router handle column
    subject text NOT NULL,
    description text NOT NULL,
    attachments text NULL,
    proposed_by integer NOT NULL,
    proposed_on timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    seconded_by integer NULL,
    seconded_on timestamp with time zone NULL,
    duration text NOT NULL, 
    status varchar(30) NOT NULL DEFAULT 'Proposed', -- Proposed, Seconded, Closed

    CONSTRAINT pk_motions PRIMARY KEY (id)
);

-- 2. Create motion_votes table
CREATE TABLE IF NOT EXISTS public.motion_votes (
    id serial NOT NULL,
    motion_id integer NOT NULL,
    voter_id integer NOT NULL,
    vote_value varchar(20) NOT NULL, -- "Yes", "No", "Abstain"
    voted_on timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),

    CONSTRAINT pk_motion_votes PRIMARY KEY (id),
    CONSTRAINT fk_motion_votes_motion FOREIGN KEY (motion_id) 
        REFERENCES public.motions (id) ON DELETE CASCADE
);

--- 
--- High-Capacity Performance Indexes
--- 

-- CRITICAL LOOKUP INDEX: Ensures lightning-fast string matching for custom application routing URLs
CREATE UNIQUE INDEX IF NOT EXISTS idx_motions_slug 
ON public.motions (slug);

-- Index to quickly load upcoming or past motions filtering by status
CREATE INDEX IF NOT EXISTS idx_motions_status 
ON public.motions (status);

-- Optimization for loading a complete overview of votes belonging to a single specific motion
CREATE INDEX IF NOT EXISTS idx_motion_votes_motion_id 
ON public.motion_votes (motion_id);

-- This unique composite index strictly ensures that a user can cast exactly ONE vote per motion.
CREATE UNIQUE INDEX IF NOT EXISTS idx_motion_votes_unique_voter_per_motion
ON public.motion_votes (motion_id, voter_id);

COMMIT;