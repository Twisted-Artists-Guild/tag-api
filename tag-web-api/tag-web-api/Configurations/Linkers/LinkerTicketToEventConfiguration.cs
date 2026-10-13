// <copyright file="LinkerTicketToEventConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models.Configurations
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using TAGWEBAPI.Models;

    public class LinkerTicketToEventConfiguration : IEntityTypeConfiguration<Linker_TicketToEvent>
    {
        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<Linker_TicketToEvent> builder)
        {
            builder.HasKey(l => l.Linker_TicketToEventID);

            builder.Property(l => l.EventID)
                .IsRequired();

            builder.Property(l => l.TicketID)
                .IsRequired();

            builder.Property(l => l.TransactionID)
                .IsRequired();
        }
    }
}
