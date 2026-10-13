-- Content Reporting & Moderation Tables

CREATE TABLE "ContentReports" (
    "ContentReportID" SERIAL PRIMARY KEY,
    "ReporterUserID" INTEGER NOT NULL,
    "TargetType" VARCHAR(50) NOT NULL,
    "TargetID" INTEGER NOT NULL,
    "TargetURL" VARCHAR(500) NULL,
    "Description" VARCHAR(2000) NOT NULL,
    "Status" VARCHAR(30) NOT NULL DEFAULT 'New',
    "Priority" INTEGER NOT NULL DEFAULT 1,
    "AssignedStaffID" INTEGER NULL,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    "ResolutionNote" VARCHAR(1000) NULL,
    CONSTRAINT "FK_ContentReports_Users" FOREIGN KEY ("ReporterUserID")
        REFERENCES "Users" ("UserID") ON DELETE CASCADE,
    CONSTRAINT "FK_ContentReports_Staffs" FOREIGN KEY ("AssignedStaffID")
        REFERENCES "Staffs" ("StaffID") ON DELETE SET NULL
);

CREATE INDEX "IX_ContentReports_Status" ON "ContentReports" ("Status");
CREATE INDEX "IX_ContentReports_TargetType_TargetID" ON "ContentReports" ("TargetType", "TargetID");
CREATE INDEX "IX_ContentReports_ReporterUserID" ON "ContentReports" ("ReporterUserID");
CREATE INDEX "IX_ContentReports_AssignedStaffID" ON "ContentReports" ("AssignedStaffID");

CREATE TABLE "ContentReportLabels" (
    "ContentReportLabelID" SERIAL PRIMARY KEY,
    "ContentReportID" INTEGER NOT NULL,
    "ContentWarningItemID" INTEGER NOT NULL,
    CONSTRAINT "FK_ContentReportLabels_Reports" FOREIGN KEY ("ContentReportID")
        REFERENCES "ContentReports" ("ContentReportID") ON DELETE CASCADE,
    CONSTRAINT "FK_ContentReportLabels_Items" FOREIGN KEY ("ContentWarningItemID")
        REFERENCES "content_warning_items" ("id") ON DELETE CASCADE
);

CREATE INDEX "IX_ContentReportLabels_ReportID" ON "ContentReportLabels" ("ContentReportID");

CREATE TABLE "ModerationActions" (
    "ModerationActionID" SERIAL PRIMARY KEY,
    "ContentReportID" INTEGER NOT NULL,
    "StaffID" INTEGER NOT NULL,
    "ActionType" VARCHAR(50) NOT NULL,
    "Note" VARCHAR(1000) NULL,
    "ActionMetadata" VARCHAR(2000) NULL,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    CONSTRAINT "FK_ModerationActions_Reports" FOREIGN KEY ("ContentReportID")
        REFERENCES "ContentReports" ("ContentReportID") ON DELETE CASCADE,
    CONSTRAINT "FK_ModerationActions_Staffs" FOREIGN KEY ("StaffID")
        REFERENCES "Staffs" ("StaffID") ON DELETE CASCADE
);

CREATE INDEX "IX_ModerationActions_ReportID" ON "ModerationActions" ("ContentReportID");

-- Add StatusID column to Blogs table
ALTER TABLE "Blogs" ADD COLUMN "StatusID" INTEGER NOT NULL DEFAULT 0;

-- Set existing blogs to Published (assuming all current blogs are live)
UPDATE "Blogs" SET "StatusID" = 2;

-- Add StatusID column to Events table
ALTER TABLE "Events" ADD COLUMN "StatusID" INTEGER NOT NULL DEFAULT 0;

-- Set existing events to Published
UPDATE "Events" SET "StatusID" = 2;