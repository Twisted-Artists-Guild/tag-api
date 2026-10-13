-- Bloomscroll Feed Tables

CREATE TABLE "FeedPosts" (
    "FeedPostID" SERIAL PRIMARY KEY,
    "AuthorUserID" INTEGER NOT NULL,
    "AuthorEntityType" VARCHAR(50) NULL,
    "AuthorEntityID" INTEGER NULL,
    "PostType" VARCHAR(50) NOT NULL DEFAULT 'General',
    "Body" VARCHAR(5000) NULL,
    "Body_Plaintext" VARCHAR(5000) NULL,
    "SharedEntityType" VARCHAR(50) NULL,
    "SharedEntityID" INTEGER NULL,
    "SharedURL" VARCHAR(500) NULL,
    "PictureID" INTEGER NULL,
    "GalleryID" INTEGER NULL,
    "IsPublished" BOOLEAN NOT NULL DEFAULT TRUE,
    "IsModerationBlocked" BOOLEAN NOT NULL DEFAULT FALSE,
    "IsSuggestedPost" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP WITH TIME ZONE NULL,
    CONSTRAINT "FK_FeedPosts_Users" FOREIGN KEY ("AuthorUserID") REFERENCES "Users" ("UserID") ON DELETE CASCADE,
    CONSTRAINT "FK_FeedPosts_Pictures" FOREIGN KEY ("PictureID") REFERENCES "Pictures" ("PictureID") ON DELETE SET NULL,
    CONSTRAINT "FK_FeedPosts_Galleries" FOREIGN KEY ("GalleryID") REFERENCES "Galleries" ("GalleryID") ON DELETE SET NULL
);

CREATE INDEX "IX_FeedPosts_AuthorUserID" ON "FeedPosts" ("AuthorUserID");
CREATE INDEX "IX_FeedPosts_CreatedAt" ON "FeedPosts" ("CreatedAt" DESC);
CREATE INDEX "IX_FeedPosts_PostType" ON "FeedPosts" ("PostType");
CREATE INDEX "IX_FeedPosts_AuthorEntity" ON "FeedPosts" ("AuthorEntityType", "AuthorEntityID");

CREATE TABLE "feed_post_impressions" (
    "id" BIGSERIAL PRIMARY KEY,
    "feed_post_id" INTEGER NOT NULL,
    "impression_id" INTEGER NOT NULL,
    "user_id" INTEGER NOT NULL,
    "created_at" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    CONSTRAINT "FK_FeedPostImpressions_Posts" FOREIGN KEY ("feed_post_id") REFERENCES "FeedPosts" ("FeedPostID") ON DELETE CASCADE,
    CONSTRAINT "FK_FeedPostImpressions_Impressions" FOREIGN KEY ("impression_id") REFERENCES "primary_impressions" ("id") ON DELETE CASCADE,
    CONSTRAINT "UQ_FeedPostImpression_User" UNIQUE ("feed_post_id", "impression_id", "user_id")
);

CREATE INDEX "IX_FeedPostImpressions_PostID" ON "feed_post_impressions" ("feed_post_id");