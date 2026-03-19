using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class SalesOrderItemConfiguration : IEntityTypeConfiguration<SalesOrderItem>
{
    public void Configure(EntityTypeBuilder<SalesOrderItem> builder)
    {
        builder.ToTable("sales_order_item");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.SalesOrderId)
            .HasColumnName("sales_order_id")
            .IsRequired();

        builder.Property(x => x.LineNumber)
            .HasColumnName("line_number")
            .IsRequired();

        builder.Property(x => x.LegacyArticleId)
            .HasColumnName("legacy_article_id")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired();

        builder.Property(x => x.Sku)
            .HasColumnName("sku")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .HasColumnType("varchar(500)")
            .IsRequired();

        builder.Property(x => x.UnitCode)
            .HasColumnName("unit_code")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired(false);

        builder.Property(x => x.UnitName)
            .HasColumnName("unit_name")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.Quantity)
            .HasColumnName("quantity")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.UnitPrice)
            .HasColumnName("unit_price")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.DiscountAmount)
            .HasColumnName("discount_amount")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.TaxRate)
            .HasColumnName("tax_rate")
            .HasPrecision(9, 6)
            .IsRequired();

        builder.Property(x => x.TaxAmount)
            .HasColumnName("tax_amount")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.LineTotal)
            .HasColumnName("line_total")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.SatProductServiceCode)
            .HasColumnName("sat_product_service_code")
            .HasMaxLength(8)
            .HasColumnType("char(8)")
            .IsRequired(false);

        builder.Property(x => x.SatUnitCode)
            .HasColumnName("sat_unit_code")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired(false);
    }
}
