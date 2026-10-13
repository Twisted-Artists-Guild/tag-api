// <copyright file="BugReportConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TAGWEBAPI.Models;

public class BugReportConfiguration : IEntityTypeConfiguration<BugReport>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BugReport> builder)
    {
        builder.ToTable("BugReport");

        builder.HasKey(bugReport => bugReport.BugReportID);

        builder.Property(bugReport => bugReport.ReporterEmail)
            .HasMaxLength(250);

        builder.Property(bugReport => bugReport.ShortDescription)
            .IsRequired()
            .HasMaxLength(180);

        builder.Property(bugReport => bugReport.ExpectedBehavior)
            .IsRequired()
            .HasMaxLength(1200);

        builder.Property(bugReport => bugReport.LongDescription)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(bugReport => bugReport.BuildNumber)
            .HasMaxLength(200);

        builder.Property(bugReport => bugReport.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("new");

        builder.Property(bugReport => bugReport.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(bugReport => bugReport.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(bugReport => bugReport.Status);
        builder.HasIndex(bugReport => bugReport.CreatedAt);
        builder.HasIndex(bugReport => bugReport.UserID);

        builder.HasOne(bugReport => bugReport.User)
            .WithMany(user => user.BugReports)
            .HasForeignKey(bugReport => bugReport.UserID)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasMany(bugReport => bugReport.StaffNotes)
            .WithOne(staffNote => staffNote.BugReport)
            .HasForeignKey(staffNote => staffNote.BugReportID)
            .OnDelete(DeleteBehavior.Cascade);
    }
}