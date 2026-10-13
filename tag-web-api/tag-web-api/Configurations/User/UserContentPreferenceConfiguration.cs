using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TAGWEBAPI.Models.Configurations;

public class UserContentPreferenceConfiguration : IEntityTypeConfiguration<UserContentPreference>
{
    public void Configure(EntityTypeBuilder<UserContentPreference> builder)
    {
        // Composite Primary Key
        builder.HasKey(p => new { p.UserId, p.ItemId });
    }
}