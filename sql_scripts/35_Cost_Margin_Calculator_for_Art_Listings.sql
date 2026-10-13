-- Cost & Margin Calculator Tables
-- Run against your TAG PostgreSQL database

-- 1. ListingCostBreakdowns
CREATE TABLE "ListingCostBreakdowns" (
    "ListingCostBreakdownID" SERIAL PRIMARY KEY,
    "ListingID" INTEGER NOT NULL,
    "PackagingCost" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "ShippingEstimate" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "InPersonPickupDiscount" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "InPersonVendingCost" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "ProfitMinAmount" DECIMAL(18,2) NULL,
    "ProfitMaxAmount" DECIMAL(18,2) NULL,
    "ProfitMinPercent" DECIMAL(5,2) NULL,
    "ProfitMaxPercent" DECIMAL(5,2) NULL,
    "ASMRP" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "FinalPrice" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "ArtistPriceOverride" BOOLEAN NOT NULL DEFAULT FALSE,
    "BelowASMRPConfirmed" BOOLEAN NOT NULL DEFAULT FALSE,
    "PriceOverrideReason" VARCHAR(500) NULL,
    "LastCalculated" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    CONSTRAINT "FK_ListingCostBreakdowns_Listings" FOREIGN KEY ("ListingID")
        REFERENCES "Listings" ("ListingID") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_ListingCostBreakdowns_ListingID" ON "ListingCostBreakdowns" ("ListingID");

-- 2. CostLineItems
CREATE TABLE "CostLineItems" (
    "CostLineItemID" SERIAL PRIMARY KEY,
    "ListingCostBreakdownID" INTEGER NOT NULL,
    "Category" VARCHAR(50) NOT NULL,
    "Description" VARCHAR(255) NOT NULL,
    "Amount" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "DisplayOrder" INTEGER NOT NULL DEFAULT 0,
    CONSTRAINT "FK_CostLineItems_ListingCostBreakdowns" FOREIGN KEY ("ListingCostBreakdownID")
        REFERENCES "ListingCostBreakdowns" ("ListingCostBreakdownID") ON DELETE CASCADE
);

CREATE INDEX "IX_CostLineItems_ListingCostBreakdownID" ON "CostLineItems" ("ListingCostBreakdownID");

-- 3. LaborEntries
CREATE TABLE "LaborEntries" (
    "LaborEntryID" SERIAL PRIMARY KEY,
    "ListingCostBreakdownID" INTEGER NOT NULL,
    "WorkerName" VARCHAR(150) NULL,
    "HourlyRate" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "HoursWorked" DECIMAL(8,2) NOT NULL DEFAULT 0,
    "Role" VARCHAR(100) NULL,
    "DisplayOrder" INTEGER NOT NULL DEFAULT 0,
    CONSTRAINT "FK_LaborEntries_ListingCostBreakdowns" FOREIGN KEY ("ListingCostBreakdownID")
        REFERENCES "ListingCostBreakdowns" ("ListingCostBreakdownID") ON DELETE CASCADE
);

CREATE INDEX "IX_LaborEntries_ListingCostBreakdownID" ON "LaborEntries" ("ListingCostBreakdownID");