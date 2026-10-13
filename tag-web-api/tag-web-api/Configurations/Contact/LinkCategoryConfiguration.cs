// <copyright file="LinkCategoryConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models.Configurations.Contact
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using TAGWEBAPI.Models;

    public class LinkCategoryConfiguration : IEntityTypeConfiguration<LinkCategory>
    {
        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<LinkCategory> builder)
        {
            builder.HasKey(lc => lc.LinkCategoryID);

            builder.Property(lc => lc.Category)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(lc => lc.CategoryKey)
                .IsRequired()
                .HasMaxLength(50);

            builder.HasIndex(lc => lc.CategoryKey)
                .IsUnique();

            builder.Property(lc => lc.Description)
                .HasMaxLength(255);

            builder.Property(lc => lc.Tags)
                .HasMaxLength(255);

            builder.HasOne(lc => lc.ParentCategory)
                .WithMany(lc => lc.SubCategories)
                .HasForeignKey(lc => lc.ParentLinkCategoryID)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            SeedData(builder);
        }

        private static void SeedData(EntityTypeBuilder<LinkCategory> builder)
        {
            builder.HasData(
                // ── Level 1: Meta Groups ───────────────────────────────────────────────────
                new LinkCategory { LinkCategoryID = 1, Category = "Social",  CategoryKey = "meta_social",  Description = "Social presence and identity links.",               Tags = "meta, social",  ParentLinkCategoryID = null },
                new LinkCategory { LinkCategoryID = 2, Category = "Create",  CategoryKey = "meta_create",  Description = "Creative platforms and development tools.",          Tags = "meta, create",  ParentLinkCategoryID = null },
                new LinkCategory { LinkCategoryID = 3, Category = "Watch",   CategoryKey = "meta_watch",   Description = "Video, streaming, and interactive media.",           Tags = "meta, watch",   ParentLinkCategoryID = null },
                new LinkCategory { LinkCategoryID = 4, Category = "Listen",  CategoryKey = "meta_listen",  Description = "Music, audio, and podcast platforms.",               Tags = "meta, listen",  ParentLinkCategoryID = null },
                new LinkCategory { LinkCategoryID = 5, Category = "Shop",    CategoryKey = "meta_shop",    Description = "Storefronts and commerce destinations.",             Tags = "meta, shop",    ParentLinkCategoryID = null },
                new LinkCategory { LinkCategoryID = 6, Category = "Support", CategoryKey = "meta_support", Description = "Payment and fan-support platforms.",                 Tags = "meta, support", ParentLinkCategoryID = null },
                new LinkCategory { LinkCategoryID = 7, Category = "Connect", CategoryKey = "meta_connect", Description = "Events, messaging, and professional connections.",  Tags = "meta, connect", ParentLinkCategoryID = null },

                // ── Level 2: Sections ─────────────────────────────────────────────────────
                new LinkCategory { LinkCategoryID = 8,  Category = "General / Identity",       CategoryKey = "general_identity",       Description = "Personal and portfolio landing pages.", Tags = "identity, profile",  ParentLinkCategoryID = 1 },
                new LinkCategory { LinkCategoryID = 9,  Category = "Social Media",             CategoryKey = "social_media",           Description = "Major social networking platforms.",    Tags = "social",             ParentLinkCategoryID = 1 },
                new LinkCategory { LinkCategoryID = 10, Category = "Creative Platforms",       CategoryKey = "creative_platforms",     Description = "Portfolio and creative showcase sites.", Tags = "create, portfolio",  ParentLinkCategoryID = 2 },
                new LinkCategory { LinkCategoryID = 11, Category = "Content & Media",          CategoryKey = "content_media",          Description = "Video, streaming, and blog platforms.", Tags = "watch, content",     ParentLinkCategoryID = 3 },
                new LinkCategory { LinkCategoryID = 12, Category = "Music & Audio",            CategoryKey = "music_audio",            Description = "Music streaming and distribution.",     Tags = "listen, music",      ParentLinkCategoryID = 4 },
                new LinkCategory { LinkCategoryID = 13, Category = "Storefronts & Commerce",   CategoryKey = "storefronts_commerce",   Description = "Online shop and marketplace platforms.", Tags = "shop, ecommerce",    ParentLinkCategoryID = 5 },
                new LinkCategory { LinkCategoryID = 14, Category = "Payments & Support",       CategoryKey = "payments_support",       Description = "Payment processors and fan support.",   Tags = "support, payments",  ParentLinkCategoryID = 6 },
                new LinkCategory { LinkCategoryID = 15, Category = "Events & Ticketing",       CategoryKey = "events_ticketing",       Description = "Event listings and ticket sales.",      Tags = "connect, events",    ParentLinkCategoryID = 7 },
                new LinkCategory { LinkCategoryID = 16, Category = "Professional / Business",  CategoryKey = "professional_business",  Description = "Professional presence and credentials.", Tags = "connect, business",  ParentLinkCategoryID = 7 },
                new LinkCategory { LinkCategoryID = 17, Category = "Messaging & Communities",  CategoryKey = "messaging_communities",  Description = "Direct messaging and group platforms.",  Tags = "connect, messaging", ParentLinkCategoryID = 7 },
                new LinkCategory { LinkCategoryID = 18, Category = "Developer / Technical",    CategoryKey = "developer_technical",    Description = "Code repositories and dev tools.",       Tags = "create, dev",        ParentLinkCategoryID = 2 },
                new LinkCategory { LinkCategoryID = 19, Category = "Publications & Writing",   CategoryKey = "publications_writing",   Description = "Long-form writing and newsletters.",     Tags = "create, writing",    ParentLinkCategoryID = 2 },
                new LinkCategory { LinkCategoryID = 20, Category = "Games & Interactive",      CategoryKey = "games_interactive",      Description = "Gaming storefronts and interactive platforms.", Tags = "watch, games", ParentLinkCategoryID = 3 },

                // ── Level 3: General / Identity (8) ──────────────────────────────────────
                new LinkCategory { LinkCategoryID = 21, Category = "Personal Website", CategoryKey = "personal_website", Description = "Your own domain or custom personal site.",            Tags = "website, personal", ParentLinkCategoryID = 8 },
                new LinkCategory { LinkCategoryID = 22, Category = "Portfolio",        CategoryKey = "portfolio",        Description = "Curated showcase of your work.",                      Tags = "portfolio",         ParentLinkCategoryID = 8 },
                new LinkCategory { LinkCategoryID = 23, Category = "Link Hub",         CategoryKey = "link_hub",         Description = "Linktree-style aggregator of all your links.",        Tags = "linktree, hub",     ParentLinkCategoryID = 8 },

                // ── Level 3: Social Media (9) ─────────────────────────────────────────────
                new LinkCategory { LinkCategoryID = 24, Category = "Instagram",   CategoryKey = "instagram",   Description = "Photo and video social platform.",                    Tags = "instagram, social",  ParentLinkCategoryID = 9 },
                new LinkCategory { LinkCategoryID = 25, Category = "TikTok",      CategoryKey = "tiktok",      Description = "Short-form video platform.",                          Tags = "tiktok, social",     ParentLinkCategoryID = 9 },
                new LinkCategory { LinkCategoryID = 26, Category = "Twitter / X",  CategoryKey = "twitter_x",   Description = "Microblogging and public conversation platform.",     Tags = "twitter, x, social", ParentLinkCategoryID = 9 },
                new LinkCategory { LinkCategoryID = 27, Category = "Facebook",    CategoryKey = "facebook",    Description = "General-purpose social networking platform.",         Tags = "facebook, social",   ParentLinkCategoryID = 9 },
                new LinkCategory { LinkCategoryID = 28, Category = "Snapchat",    CategoryKey = "snapchat",    Description = "Ephemeral photo and video messaging app.",            Tags = "snapchat, social",   ParentLinkCategoryID = 9 },
                new LinkCategory { LinkCategoryID = 29, Category = "Threads",     CategoryKey = "threads",     Description = "Meta's text-based social platform.",                  Tags = "threads, social",    ParentLinkCategoryID = 9 },
                new LinkCategory { LinkCategoryID = 30, Category = "Mastodon",    CategoryKey = "mastodon",    Description = "Decentralized open-source social network.",           Tags = "mastodon, fediverse", ParentLinkCategoryID = 9 },

                // ── Level 3: Creative Platforms (10) ─────────────────────────────────────
                new LinkCategory { LinkCategoryID = 31, Category = "Behance",    CategoryKey = "behance",    Description = "Adobe's creative portfolio network.",                  Tags = "behance, portfolio", ParentLinkCategoryID = 10 },
                new LinkCategory { LinkCategoryID = 32, Category = "ArtStation", CategoryKey = "artstation", Description = "Portfolio platform for game, film, and media artists.", Tags = "artstation",         ParentLinkCategoryID = 10 },
                new LinkCategory { LinkCategoryID = 33, Category = "Dribbble",   CategoryKey = "dribbble",   Description = "Design community for UI, graphic, and web designers.", Tags = "dribbble, design",   ParentLinkCategoryID = 10 },
                new LinkCategory { LinkCategoryID = 34, Category = "DeviantArt", CategoryKey = "deviantart", Description = "Online art community for fan art and original works.",  Tags = "deviantart",         ParentLinkCategoryID = 10 },
                new LinkCategory { LinkCategoryID = 35, Category = "Pixiv",      CategoryKey = "pixiv",      Description = "Japanese illustration and manga community.",            Tags = "pixiv, illustration", ParentLinkCategoryID = 10 },

                // ── Level 3: Content & Media (11) ────────────────────────────────────────
                new LinkCategory { LinkCategoryID = 36, Category = "YouTube", CategoryKey = "youtube",  Description = "Video hosting and streaming platform.",    Tags = "youtube, video",   ParentLinkCategoryID = 11 },
                new LinkCategory { LinkCategoryID = 37, Category = "Vimeo",   CategoryKey = "vimeo",    Description = "High-quality video hosting for creators.",  Tags = "vimeo, video",     ParentLinkCategoryID = 11 },
                new LinkCategory { LinkCategoryID = 38, Category = "Twitch",  CategoryKey = "twitch",   Description = "Live streaming platform.",                  Tags = "twitch, streaming", ParentLinkCategoryID = 11 },
                new LinkCategory { LinkCategoryID = 39, Category = "Podcast", CategoryKey = "podcast",  Description = "Audio podcast feed or hosting page.",       Tags = "podcast, audio",   ParentLinkCategoryID = 11 },
                new LinkCategory { LinkCategoryID = 40, Category = "Blog",    CategoryKey = "blog",     Description = "Personal or professional blog.",            Tags = "blog, writing",    ParentLinkCategoryID = 11 },

                // ── Level 3: Music & Audio (12) ──────────────────────────────────────────
                new LinkCategory { LinkCategoryID = 41, Category = "Spotify",     CategoryKey = "spotify",     Description = "Music and podcast streaming platform.",         Tags = "spotify, music",    ParentLinkCategoryID = 12 },
                new LinkCategory { LinkCategoryID = 42, Category = "SoundCloud",  CategoryKey = "soundcloud",  Description = "Audio distribution and discovery platform.",    Tags = "soundcloud, music", ParentLinkCategoryID = 12 },
                new LinkCategory { LinkCategoryID = 43, Category = "Apple Music", CategoryKey = "apple_music", Description = "Apple's music streaming service.",              Tags = "applemusic, music", ParentLinkCategoryID = 12 },
                new LinkCategory { LinkCategoryID = 44, Category = "Bandcamp",    CategoryKey = "bandcamp",    Description = "Direct music sales and fan-support platform.",  Tags = "bandcamp, music",   ParentLinkCategoryID = 12 },
                new LinkCategory { LinkCategoryID = 45, Category = "Mixcloud",    CategoryKey = "mixcloud",    Description = "DJ mixes, radio shows, and podcasts.",          Tags = "mixcloud, dj",      ParentLinkCategoryID = 12 },

                // ── Level 3: Storefronts & Commerce (13) ─────────────────────────────────
                new LinkCategory { LinkCategoryID = 46, Category = "Shopify",    CategoryKey = "shopify",    Description = "Full-featured ecommerce platform.",              Tags = "shopify, ecommerce", ParentLinkCategoryID = 13 },
                new LinkCategory { LinkCategoryID = 47, Category = "Etsy",       CategoryKey = "etsy",       Description = "Marketplace for handmade and vintage goods.",    Tags = "etsy, shop",         ParentLinkCategoryID = 13 },
                new LinkCategory { LinkCategoryID = 48, Category = "BigCartel",  CategoryKey = "bigcartel",  Description = "Simple storefronts for artists and makers.",     Tags = "bigcartel, shop",    ParentLinkCategoryID = 13 },
                new LinkCategory { LinkCategoryID = 49, Category = "Gumroad",    CategoryKey = "gumroad",    Description = "Digital products and creator commerce.",         Tags = "gumroad, digital",   ParentLinkCategoryID = 13 },
                new LinkCategory { LinkCategoryID = 50, Category = "WooCommerce",CategoryKey = "woocommerce", Description = "WordPress-based ecommerce solution.",           Tags = "woocommerce, shop",  ParentLinkCategoryID = 13 },

                // ── Level 3: Payments & Support (14) ─────────────────────────────────────
                new LinkCategory { LinkCategoryID = 51, Category = "PayPal",         CategoryKey = "paypal",         Description = "Online payment transfer service.",            Tags = "paypal, payments",   ParentLinkCategoryID = 14 },
                new LinkCategory { LinkCategoryID = 52, Category = "Venmo",          CategoryKey = "venmo",          Description = "Mobile peer-to-peer payments.",               Tags = "venmo, payments",    ParentLinkCategoryID = 14 },
                new LinkCategory { LinkCategoryID = 53, Category = "Cash App",       CategoryKey = "cash_app",       Description = "Mobile money transfer app.",                  Tags = "cashapp, payments",  ParentLinkCategoryID = 14 },
                new LinkCategory { LinkCategoryID = 54, Category = "Zelle",          CategoryKey = "zelle",          Description = "Bank-integrated money transfer network.",     Tags = "zelle, payments",    ParentLinkCategoryID = 14 },
                new LinkCategory { LinkCategoryID = 55, Category = "Stripe",         CategoryKey = "stripe",         Description = "Developer-friendly payment processing.",      Tags = "stripe, payments",   ParentLinkCategoryID = 14 },
                new LinkCategory { LinkCategoryID = 56, Category = "Square",         CategoryKey = "square",         Description = "Payment processing and POS platform.",        Tags = "square, payments",   ParentLinkCategoryID = 14 },
                new LinkCategory { LinkCategoryID = 57, Category = "Patreon",        CategoryKey = "patreon",        Description = "Recurring fan membership and support.",       Tags = "patreon, support",   ParentLinkCategoryID = 14 },
                new LinkCategory { LinkCategoryID = 58, Category = "Ko-fi",          CategoryKey = "kofi",           Description = "One-time or recurring creator support.",      Tags = "kofi, support",      ParentLinkCategoryID = 14 },
                new LinkCategory { LinkCategoryID = 59, Category = "Buy Me a Coffee",CategoryKey = "buy_me_a_coffee",Description = "Micro-donation and supporter platform.",      Tags = "bmac, support",      ParentLinkCategoryID = 14 },

                // ── Level 3: Events & Ticketing (15) ─────────────────────────────────────
                new LinkCategory { LinkCategoryID = 60, Category = "Eventbrite",     CategoryKey = "eventbrite",     Description = "Event creation and ticket sales platform.",  Tags = "eventbrite, events", ParentLinkCategoryID = 15 },
                new LinkCategory { LinkCategoryID = 61, Category = "Ticketing Page", CategoryKey = "ticketing_page", Description = "Custom or third-party ticketing page.",      Tags = "tickets, events",    ParentLinkCategoryID = 15 },
                new LinkCategory { LinkCategoryID = 62, Category = "Booking Page",   CategoryKey = "booking_page",   Description = "Scheduling or booking appointment page.",   Tags = "booking, events",    ParentLinkCategoryID = 15 },

                // ── Level 3: Professional / Business (16) ────────────────────────────────
                new LinkCategory { LinkCategoryID = 63, Category = "LinkedIn",             CategoryKey = "linkedin",           Description = "Professional networking platform.",                Tags = "linkedin, professional", ParentLinkCategoryID = 16 },
                new LinkCategory { LinkCategoryID = 64, Category = "Resume / CV",          CategoryKey = "resume_cv",          Description = "Downloadable or hosted resume or CV.",             Tags = "resume, cv",             ParentLinkCategoryID = 16 },
                new LinkCategory { LinkCategoryID = 65, Category = "Press Kit",            CategoryKey = "press_kit",          Description = "Media and press kit for professional outreach.",   Tags = "presskit, epk",          ParentLinkCategoryID = 16 },
                new LinkCategory { LinkCategoryID = 66, Category = "Booking / Contact Page",CategoryKey = "booking_contact",   Description = "Page for booking inquiries or professional contact.", Tags = "booking, contact",     ParentLinkCategoryID = 16 },

                // ── Level 3: Messaging & Communities (17) ────────────────────────────────
                new LinkCategory { LinkCategoryID = 67, Category = "Discord",   CategoryKey = "discord",   Description = "Community server or direct invite link.",  Tags = "discord, community", ParentLinkCategoryID = 17 },
                new LinkCategory { LinkCategoryID = 68, Category = "Telegram",  CategoryKey = "telegram",  Description = "Messaging app channel or group link.",     Tags = "telegram, messaging", ParentLinkCategoryID = 17 },
                new LinkCategory { LinkCategoryID = 69, Category = "WhatsApp",  CategoryKey = "whatsapp",  Description = "WhatsApp group or direct message link.",   Tags = "whatsapp, messaging", ParentLinkCategoryID = 17 },
                new LinkCategory { LinkCategoryID = 70, Category = "Slack",     CategoryKey = "slack",     Description = "Workspace or community Slack invite.",     Tags = "slack, community",   ParentLinkCategoryID = 17 },

                // ── Level 3: Developer / Technical (18) ──────────────────────────────────
                new LinkCategory { LinkCategoryID = 71, Category = "GitHub",  CategoryKey = "github",  Description = "Git repository hosting and open-source collaboration.", Tags = "github, dev", ParentLinkCategoryID = 18 },
                new LinkCategory { LinkCategoryID = 72, Category = "GitLab",  CategoryKey = "gitlab",  Description = "DevOps and repository platform.",                        Tags = "gitlab, dev", ParentLinkCategoryID = 18 },
                new LinkCategory { LinkCategoryID = 73, Category = "CodePen", CategoryKey = "codepen", Description = "Online code editor and front-end showcase.",             Tags = "codepen, dev", ParentLinkCategoryID = 18 },

                // ── Level 3: Publications & Writing (19) ─────────────────────────────────
                new LinkCategory { LinkCategoryID = 74, Category = "Medium",     CategoryKey = "medium",     Description = "Online publishing platform for articles and essays.", Tags = "medium, writing",     ParentLinkCategoryID = 19 },
                new LinkCategory { LinkCategoryID = 75, Category = "Substack",   CategoryKey = "substack",   Description = "Newsletter and subscription writing platform.",       Tags = "substack, newsletter", ParentLinkCategoryID = 19 },
                new LinkCategory { LinkCategoryID = 76, Category = "Newsletter", CategoryKey = "newsletter",  Description = "Direct email newsletter or mailing list link.",      Tags = "newsletter, email",   ParentLinkCategoryID = 19 },

                // ── Level 3: Games & Interactive (20) ────────────────────────────────────
                new LinkCategory { LinkCategoryID = 77, Category = "Steam",   CategoryKey = "steam",   Description = "PC gaming platform by Valve.",                   Tags = "steam, games", ParentLinkCategoryID = 20 },
                new LinkCategory { LinkCategoryID = 78, Category = "itch.io", CategoryKey = "itch_io", Description = "Indie game marketplace and creator platform.",   Tags = "itchio, games", ParentLinkCategoryID = 20 },

                // ── Additional platforms ──────────────────────────────────────────────────
                new LinkCategory { LinkCategoryID = 79, Category = "Reddit",   CategoryKey = "reddit",  Description = "Community-driven social news and discussion platform.", Tags = "reddit, social", ParentLinkCategoryID = 9 }
            );
        }
    }
}
