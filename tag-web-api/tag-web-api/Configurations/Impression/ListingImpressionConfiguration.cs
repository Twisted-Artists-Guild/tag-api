using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations;

public class ListingImpressionConfiguration : IEntityTypeConfiguration<ListingImpression>
{
    public void Configure(EntityTypeBuilder<ListingImpression> builder)
    {
        builder.HasKey(li => li.Id);

        // Composite Unique Index: User + Listing + Specific Emoji
        builder.HasIndex(li => new { li.ListingId, li.UserId, li.ImpressionId })
            .IsUnique();
    }
}