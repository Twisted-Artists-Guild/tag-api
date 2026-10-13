using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations;

public class ContestEntryConfiguration : IEntityTypeConfiguration<ContestEntry>
{
    public void Configure(EntityTypeBuilder<ContestEntry> builder)
    {
        builder.HasKey(ce => ce.Id);

        // Prevents the same Art/Listing from entering the same Contest twice
        builder.HasIndex(ce => new { ce.ContestId, ce.ListingId })
            .IsUnique();

        builder.HasOne(ce => ce.Contest)
            .WithMany(c => c.Entries)
            .HasForeignKey(ce => ce.ContestId);
    }
}