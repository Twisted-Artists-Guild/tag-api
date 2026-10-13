BEGIN;

-- 1. Create carts table
CREATE TABLE IF NOT EXISTS public.carts (
    id serial NOT NULL,
    user_id integer NULL, -- Nullable to fully support your Guest Cart workflow
    session_id varchar(255) NULL, -- Target identifier for cookie/session tracking
    created_at timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_at timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),

    CONSTRAINT pk_carts PRIMARY KEY (id)
);

-- 2. Create cart_items table
CREATE TABLE IF NOT EXISTS public.cart_items (
    id serial NOT NULL,
    cart_id integer NOT NULL,
    listing_id integer NOT NULL,
    quantity integer NOT NULL DEFAULT 1,
    added_at timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),

    CONSTRAINT pk_cart_items PRIMARY KEY (id),
    
    -- Deleting a cart instantly cleans up its associated line items
    CONSTRAINT fk_cart_items_cart FOREIGN KEY (cart_id) 
        REFERENCES public.carts (id) ON DELETE CASCADE
);

--- 
--- Performance Optimization Indexes
--- 

-- High-Speed Core Lookups: Speeds up checking if a logged-in user has an active cart
CREATE INDEX IF NOT EXISTS idx_carts_user_id 
ON public.carts (user_id) 
WHERE user_id IS NOT NULL;

-- High-Speed Guest Lookups: Optimizes finding active carts for anonymous visitors via session cookies
CREATE INDEX IF NOT EXISTS idx_carts_session_id 
ON public.carts (session_id) 
WHERE session_id IS NOT NULL;

-- Cart Management Optimization: Speeds up loading all items when a user opens their shopping cart layout
CREATE INDEX IF NOT EXISTS idx_cart_items_cart_id 
ON public.cart_items (cart_id);

-- 1. Create orders table
CREATE TABLE IF NOT EXISTS public.orders (
    id serial NOT NULL,
    user_id integer NOT NULL,
    order_number varchar(100) NOT NULL,
    stripe_payment_intent_id varchar(255) NULL,
    total_cents integer NOT NULL, -- Storing currency as integers/cents to prevent floating-point errors
    status varchar(50) NOT NULL DEFAULT 'Processing', -- Processing, Shipped, Delivered, Refunded
    created_at timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),

    CONSTRAINT pk_orders PRIMARY KEY (id),
    CONSTRAINT uq_orders_order_number UNIQUE (order_number) -- Prevents duplicate order identifiers
);

-- 2. Create order_items table
CREATE TABLE IF NOT EXISTS public.order_items (
    id serial NOT NULL,
    order_id integer NOT NULL,
    listing_id integer NOT NULL,
    quantity integer NOT NULL,
    unit_price_cents integer NOT NULL, -- Captures the exact historical price at checkout time

    CONSTRAINT pk_order_items PRIMARY KEY (id),
    
    -- Cascade delete ensures line items clear up if an unfulfilled order record is purged
    CONSTRAINT fk_order_items_order FOREIGN KEY (order_id) 
        REFERENCES public.orders (id) ON DELETE CASCADE
);

--- 
--- Performance & Fulfillment Indexes
--- 

-- Stripe Webhook Optimization: Fast lookup when Stripe sends asynchronous payment intent updates
CREATE INDEX IF NOT EXISTS idx_orders_stripe_intent 
ON public.orders (stripe_payment_intent_id) 
WHERE stripe_payment_intent_id IS NOT NULL;

-- Customer Order History: Accelerates querying orders belonging to a single user profile
CREATE INDEX IF NOT EXISTS idx_orders_user_id 
ON public.orders (user_id);

-- Operational Fulfillment Filter: For dashboard queues filtering by Processing, Shipped, etc.
CREATE INDEX IF NOT EXISTS idx_orders_status 
ON public.orders (status);

-- Order Item Hydration: Fast loading of line items when a user views their receipt details
CREATE INDEX IF NOT EXISTS idx_order_items_order_id 
ON public.order_items (order_id);


-- 1. Add shipping_label_url column
ALTER TABLE public.orders 
ADD COLUMN shipping_label_url text NULL;

-- 2. Add tracking_number column
ALTER TABLE public.orders 
ADD COLUMN tracking_number varchar(100) NULL;

---
--- Performance Index
---

-- High-Speed Tracking Lookups: Optimizes customer dashboard queries 
-- when tracking orders or processing webhook delivery updates.
CREATE INDEX IF NOT EXISTS idx_orders_tracking_number 
ON public.orders (tracking_number) 
WHERE tracking_number IS NOT NULL;

COMMIT;