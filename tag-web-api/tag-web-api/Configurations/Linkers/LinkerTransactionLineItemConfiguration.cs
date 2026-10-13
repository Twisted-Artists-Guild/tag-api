using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Models.Configurations;

public class LinkerTransactionLineItemConfiguration : IEntityTypeConfiguration<Linker_TransactionLineItem>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Linker_TransactionLineItem> builder)
    {
        builder.HasKey(l => l.Linker_TransactionLineItemID);

        builder.Property(l => l.Discount)
            .HasColumnType("decimal(18,2)");

        builder.Property(l => l.DiscountReason)
            .HasMaxLength(255);

        builder.Property(l => l.FinalSalesPrice)
            .HasColumnType("decimal(18,2)");

        builder.Property(l => l.LineItemSubtotal)
            .HasColumnType("decimal(18,2)");

        builder.Property(l => l.Note)
            .HasMaxLength(1000);

        builder.Property(l => l.Quantity)
            .HasColumnType("decimal(18,2)");

        builder.Property(l => l.Tax)
            .HasColumnType("decimal(18,2)");

        builder.HasOne(l => l.Listing)
            .WithMany()
            .HasForeignKey(l => l.ListingID);

        builder.HasOne(l => l.Transaction)
            .WithMany()
            .HasForeignKey(l => l.TransactionID);
    }
}
