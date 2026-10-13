using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations;

public class PrimaryImpressionConfiguration : IEntityTypeConfiguration<PrimaryImpression>
{
    public void Configure(EntityTypeBuilder<PrimaryImpression> builder)
    {
        builder.HasKey(m => m.Id);

        builder.HasData(
            new PrimaryImpression { Id = 1, Emoji = "❤️", Name = "love", Label = "Love", DisplayOrder = 1 },
            new PrimaryImpression { Id = 2, Emoji = "👏", Name = "applause", Label = "Applause", DisplayOrder = 2 },
            new PrimaryImpression { Id = 3, Emoji = "🔥", Name = "fire", Label = "Fire", DisplayOrder = 3 },
            new PrimaryImpression { Id = 4, Emoji = "🙏", Name = "gratitude", Label = "Gratitude", DisplayOrder = 4 },
            new PrimaryImpression { Id = 5, Emoji = "🎨", Name = "art", Label = "Art", DisplayOrder = 5 },
            new PrimaryImpression { Id = 6, Emoji = "✨", Name = "sparkles", Label = "Sparkles", DisplayOrder = 6 },
            new PrimaryImpression { Id = 7, Emoji = "👀", Name = "eyes", Label = "Eyes", DisplayOrder = 7 },
            new PrimaryImpression { Id = 8, Emoji = "💯", Name = "hundred", Label = "Hundred", DisplayOrder = 8 },
            new PrimaryImpression { Id = 9, Emoji = "🤩", Name = "star_struck", Label = "Star Struck", DisplayOrder = 9 },
            new PrimaryImpression { Id = 10, Emoji = "🙌", Name = "praise", Label = "Praise", DisplayOrder = 10 }
            // Add the remaining 2 based on your requirements
        );
    }
}