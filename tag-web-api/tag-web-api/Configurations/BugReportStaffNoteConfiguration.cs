// <copyright file="BugReportStaffNoteConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TAGWEBAPI.Models;

public class BugReportStaffNoteConfiguration : IEntityTypeConfiguration<BugReportStaffNote>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BugReportStaffNote> builder)
    {
        builder.ToTable("BugReportStaffNote");

        builder.HasKey(staffNote => staffNote.BugReportStaffNoteID);

        builder.Property(staffNote => staffNote.Note)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(staffNote => staffNote.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(staffNote => staffNote.BugReportID);
        builder.HasIndex(staffNote => staffNote.CreatedAt);

        builder.HasOne(staffNote => staffNote.BugReport)
            .WithMany(bugReport => bugReport.StaffNotes)
            .HasForeignKey(staffNote => staffNote.BugReportID)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(staffNote => staffNote.Staff)
            .WithMany()
            .HasForeignKey(staffNote => staffNote.StaffID)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}