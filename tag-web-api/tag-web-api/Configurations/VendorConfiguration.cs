// <copyright file="VendorConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models.Configurations
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using TAGWEBAPI.Models;

    public class VendorConfiguration : IEntityTypeConfiguration<Vendor>
    {
        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<Vendor> builder)
        {
            builder.HasKey(v => v.VendorID);

            builder.Property(v => v.CompanyName)
                .HasMaxLength(1000);

            builder.Property(v => v.ContractExpires)
                .HasColumnType("timestamptz");

            builder.Property(v => v.LinkToContract)
                .HasMaxLength(1000);

            builder.Property(v => v.LinkToMSA)
                .HasMaxLength(1000);

            builder.Property(v => v.MSA_Executed)
                .HasColumnType("timestamptz");

            builder.Property(v => v.NotesOnContracts)
                .HasColumnType("text");

            builder.Property(v => v.NotesOnVendors)
                .HasColumnType("text");

            builder.Property(v => v.POCEmail)
                .HasColumnType("text");

            builder.Property(v => v.POCName)
                .HasColumnType("text");

            builder.Property(v => v.POCPhone)
                .HasColumnType("text");

            builder.Property(v => v.IsPublished)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(v => v.IsModerationBlocked)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(v => v.LogoPicID)
                .IsRequired(false);

            builder.HasOne(v => v.LogoPic)
                .WithMany()
                .HasForeignKey(v => v.LogoPicID)
                .OnDelete(DeleteBehavior.SetNull);

        }
    }
}
