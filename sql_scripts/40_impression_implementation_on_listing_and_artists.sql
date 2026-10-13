INSERT INTO public.primary_impressions (id, emoji, name, label, display_order)
VALUES 
    (1, '❤️', 'love', 'Love', 1),
    (2, '👏', 'applause', 'Applause', 2),
    (3, '🔥', 'fire', 'Fire', 3),
    (4, '🙏', 'gratitude', 'Gratitude', 4),
    (5, '🎨', 'art', 'Art', 5),
    (6, '✨', 'sparkles', 'Sparkles', 6),
    (7, '👀', 'eyes', 'Eyes', 7),
    (8, '💯', 'hundred', 'Hundred', 8),
    (9, '🤩', 'star_struck', 'Star Struck', 9),
    (10, '🙌', 'praise', 'Praise', 10)


	-- Create table
CREATE TABLE IF NOT EXISTS public.artist_impressions (
    id bigserial NOT NULL,
    artist_id integer NOT NULL,
    impression_id integer NOT NULL,
    user_id integer NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    
    -- Primary Key Constraint
    CONSTRAINT pk_artist_impressions PRIMARY KEY (id)
);