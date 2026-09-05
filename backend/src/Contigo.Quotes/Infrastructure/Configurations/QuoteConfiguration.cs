using Contigo.Quotes.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Contigo.Quotes.Infrastructure.Configurations;

public sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("quote");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasConversion(ValueConverters.EntityIdConverter)
            .ValueGeneratedNever();
        builder.Property(e => e.TenantId)
            .HasConversion(ValueConverters.TenantIdConverter);

        builder.Property(e => e.FileName).IsRequired().HasMaxLength(500);
        builder.Property(e => e.MimeType).IsRequired().HasMaxLength(200);
        builder.Property(e => e.StoragePath).IsRequired().HasMaxLength(1000);
        builder.Property(e => e.Checksum).IsRequired().HasMaxLength(128);

        // Same enum-as-string convention as every other module's own closed-set columns (e.g.
        // Contigo.Savings.Infrastructure.Configurations.SavingsOpportunityConfiguration's
        // `Status`).
        builder.Property(e => e.ProcessingStatus).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(e => e.TenantId);
    }
}
