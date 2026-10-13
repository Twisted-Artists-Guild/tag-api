-- Create comments table
CREATE TABLE IF NOT EXISTS public.comments (
    id BIGSERIAL PRIMARY KEY,
    target_type INTEGER NOT NULL,
    target_id INTEGER NOT NULL,
    user_id INTEGER NOT NULL,
    content VARCHAR(2000) NOT NULL,
    parent_comment_id BIGINT NULL,
    is_edited BOOLEAN DEFAULT FALSE,
    is_deleted BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at TIMESTAMP WITHOUT TIME ZONE NULL,
    CONSTRAINT fk_parent_comment FOREIGN KEY (parent_comment_id) REFERENCES public.comments(id) ON DELETE CASCADE
);

-- Create indexes for better performance
CREATE INDEX idx_comments_target ON public.comments(target_type, target_id, created_at DESC) WHERE is_deleted = FALSE;
CREATE INDEX idx_comments_parent ON public.comments(parent_comment_id, created_at) WHERE is_deleted = FALSE AND parent_comment_id IS NOT NULL;
CREATE INDEX idx_comments_user ON public.comments(user_id, created_at DESC) WHERE is_deleted = FALSE;

-- Add comment for enum values
COMMENT ON COLUMN public.comments.target_type IS '1=Artist, 2=Listing, 3=Blog, 4=News';


-- Create comment_impressions table
CREATE TABLE IF NOT EXISTS public.comment_impressions (
    id BIGSERIAL PRIMARY KEY,
    comment_id BIGINT NOT NULL,
    impression_id INTEGER NOT NULL,
    user_id INTEGER NOT NULL,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT fk_comment FOREIGN KEY (comment_id) REFERENCES public.comments(id) ON DELETE CASCADE,
    CONSTRAINT fk_impression FOREIGN KEY (impression_id) REFERENCES public.primary_impressions(id) ON DELETE CASCADE,
    CONSTRAINT uk_comment_user_impression UNIQUE (comment_id, user_id, impression_id)
);

-- Create indexes for better performance
CREATE INDEX idx_comment_impressions_comment ON public.comment_impressions(comment_id, impression_id);
CREATE INDEX idx_comment_impressions_user ON public.comment_impressions(user_id);

-- Add comment for enum value
COMMENT ON COLUMN public.comment_impressions.impression_id IS 'References primary_impressions.id';

-- Create blog_impressions table
CREATE TABLE IF NOT EXISTS public.blog_impressions (
    id BIGSERIAL PRIMARY KEY,
    blog_id INTEGER NOT NULL,
    impression_id INTEGER NOT NULL,
    user_id INTEGER NOT NULL,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT fk_impression FOREIGN KEY (impression_id) REFERENCES public.primary_impressions(id) ON DELETE CASCADE,
    CONSTRAINT uk_blog_user_impression UNIQUE (blog_id, user_id, impression_id)
);

-- Create indexes for better performance
CREATE INDEX idx_blog_impressions_blog ON public.blog_impressions(blog_id, impression_id);
CREATE INDEX idx_blog_impressions_user ON public.blog_impressions(user_id);

-- Add comment for reference
COMMENT ON COLUMN public.blog_impressions.impression_id IS 'References primary_impressions.id';
COMMENT ON TABLE public.blog_impressions IS 'Stores user impressions/reactions on blog posts';