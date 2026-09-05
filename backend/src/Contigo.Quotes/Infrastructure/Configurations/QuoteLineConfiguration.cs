using Contigo.Quotes.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Contigo.Quotes.Infrastructure.Configurations;

public sealed class QuoteLineConfiguration : IEntityTypeConfiguration<QuoteLine>
{
    public void Configure(EntityTypeBuilder<QuoteLine> builder)
    {
        builder.ToTable("quote_line");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasConversion(ValueConverters.EntityIdConverter)
            .ValueGeneratedNever();
        builder.Property(e => e.TenantId)
            .HasConversion(ValueConverters.TenantIdConverter);

        builder.Property(e => e.QuoteId)
            .HasConversion(ValueConverters.EntityIdConverter);

        builder.Property(e => e.Sku).HasMaxLength(100);
        builder.Property(e => e.Edition).HasMaxLength(100);
        builder.Property(e => e.Description).IsRequired().HasMaxLength(1000);
        builder.Property(e => e.Unit).HasMaxLength(50);
        builder.Property(e => e.Term).HasMaxLength(100);

        builder.Property(e => e.Quantity).HasPrecision(18, 4);
        builder.Property(e => e.UnitPrice).HasPrecision(18, 2);
        builder.Property(e => e.ListPrice).HasPrecision(18, 2);
        builder.Property(e => e.DiscountPercent).HasPrecision(9, 4);
        builder.Property(e => e.ExtendedPrice).HasPrecision(18, 2);

        builder.Property(e => e.SourceSpan).HasMaxLength(2000);

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.TenantId, e.QuoteId });
    }
}
