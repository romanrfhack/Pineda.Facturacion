from pathlib import Path

path = Path('.github/agent/legacy-payment-prefill.py')
text = path.read_text(encoding='utf-8')
marker = '''replace_once(
    "src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Configurations/SalesOrderConfiguration.cs",'''
start = text.index(marker)
end = text.index('\n\nreplace_once(', start + len(marker))
new_call = '''replace_once(
    "src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Configurations/SalesOrderConfiguration.cs",
    "        builder.Property(x => x.PaymentCondition)\\n            .HasColumnName(\\\"payment_condition\\\")\\n            .HasMaxLength(10)\\n            .HasColumnType(\\\"varchar(10)\\\")\\n            .IsRequired();\\n\\n        builder.Property(x => x.PriceListCode)",
    "        builder.Property(x => x.PaymentCondition)\\n            .HasColumnName(\\\"payment_condition\\\")\\n            .HasMaxLength(10)\\n            .HasColumnType(\\\"varchar(10)\\\")\\n            .IsRequired();\\n\\n        builder.Property(x => x.LegacyPaymentCode)\\n            .HasColumnName(\\\"legacy_payment_code\\\")\\n            .HasMaxLength(15)\\n            .HasColumnType(\\\"varchar(15)\\\")\\n            .IsRequired(false);\\n\\n        builder.Property(x => x.LegacyPaymentDescription)\\n            .HasColumnName(\\\"legacy_payment_description\\\")\\n            .HasMaxLength(60)\\n            .HasColumnType(\\\"varchar(60)\\\")\\n            .IsRequired(false);\\n\\n        builder.Property(x => x.PriceListCode)")'''
text = text[:start] + new_call + text[end:]
path.write_text(text, encoding='utf-8')
print('Adjusted SalesOrderConfiguration patch.')
