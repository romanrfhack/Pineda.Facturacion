from pathlib import Path


def read(path: str) -> str:
    return Path(path).read_text(encoding="utf-8-sig")


def write(path: str, content: str) -> None:
    Path(path).write_text(content, encoding="utf-8")


def replace_once(path: str, old: str, new: str) -> None:
    text = read(path)
    if old not in text:
        raise RuntimeError(f"Expected snippet not found in {path}: {old[:160]!r}")
    write(path, text.replace(old, new, 1))


# ImportLegacyOrderService must use only the primary ILegacyOrderReader.
# The primary reader now carries the advisory legacy payment evidence in the same read,
# which preserves integration-test isolation and avoids a second Legacy DB connection.
import_service = "src/Pineda.Facturacion.Application/UseCases/ImportLegacyOrder/ImportLegacyOrderService.cs"
replace_once(
    import_service,
    "    private readonly ILegacyOrderReader _legacyOrderReader;\n    private readonly ILegacyOrderPaymentReader? _legacyOrderPaymentReader;\n    private readonly LegacyImportRevisionRecorder _legacyImportRevisionRecorder;",
    "    private readonly ILegacyOrderReader _legacyOrderReader;\n    private readonly LegacyImportRevisionRecorder _legacyImportRevisionRecorder;")
replace_once(
    import_service,
    "        IContentHashGenerator contentHashGenerator,\n        LegacyImportRevisionRecorder legacyImportRevisionRecorder,\n        ILegacyOrderPaymentReader? legacyOrderPaymentReader = null)",
    "        IContentHashGenerator contentHashGenerator,\n        LegacyImportRevisionRecorder legacyImportRevisionRecorder)")
replace_once(
    import_service,
    "        _contentHashGenerator = contentHashGenerator;\n        _legacyImportRevisionRecorder = legacyImportRevisionRecorder;\n        _legacyOrderPaymentReader = legacyOrderPaymentReader;",
    "        _contentHashGenerator = contentHashGenerator;\n        _legacyImportRevisionRecorder = legacyImportRevisionRecorder;")
replace_once(
    import_service,
    '''        var sourceHash = _contentHashGenerator.GenerateHash(legacyOrder);
        if (_legacyOrderPaymentReader is not null)
        {
            var legacyPayment = await _legacyOrderPaymentReader.GetByOrderIdAsync(
                legacyOrder.LegacyOrderId,
                cancellationToken);
            legacyOrder.LegacyPaymentCode = legacyPayment?.LegacyPaymentCode;
            legacyOrder.LegacyPaymentDescription = legacyPayment?.LegacyPaymentDescription;
        }

        var existingImportRecord = await _legacyImportRecordRepository.GetBySourceDocumentAsync(''',
    '''        var sourceHash = _contentHashGenerator.GenerateHash(legacyOrder);
        var existingImportRecord = await _legacyImportRecordRepository.GetBySourceDocumentAsync(''')

# Reimport uses the same primary reader. Keep the replacement-snapshot assignments added by
# the main patch, but remove the extra auxiliary-reader dependency/call.
reimport_service = "src/Pineda.Facturacion.Application/UseCases/ImportLegacyOrder/ReimportLegacyOrderService.cs"
replace_once(
    reimport_service,
    "    private readonly ILegacyOrderReader _legacyOrderReader;\n    private readonly ILegacyOrderPaymentReader? _legacyOrderPaymentReader;\n    private readonly IContentHashGenerator _contentHashGenerator;",
    "    private readonly ILegacyOrderReader _legacyOrderReader;\n    private readonly IContentHashGenerator _contentHashGenerator;")
replace_once(
    reimport_service,
    "        IProductFiscalProfileRepository productFiscalProfileRepository,\n        IUnitOfWork unitOfWork,\n        ILegacyOrderPaymentReader? legacyOrderPaymentReader = null)",
    "        IProductFiscalProfileRepository productFiscalProfileRepository,\n        IUnitOfWork unitOfWork)")
replace_once(
    reimport_service,
    "        _productFiscalProfileRepository = productFiscalProfileRepository;\n        _unitOfWork = unitOfWork;\n        _legacyOrderPaymentReader = legacyOrderPaymentReader;",
    "        _productFiscalProfileRepository = productFiscalProfileRepository;\n        _unitOfWork = unitOfWork;")
replace_once(
    reimport_service,
    '''        if (!string.Equals(applyHash, preview.CurrentSourceHash, StringComparison.Ordinal))
        {
            return PreviewExpiredConflict(preview, "Legacy source data changed after the preview. Refresh the preview before retrying reimport.");
        }

        if (_legacyOrderPaymentReader is not null)
        {
            var legacyPayment = await _legacyOrderPaymentReader.GetByOrderIdAsync(
                currentLegacyOrder.LegacyOrderId,
                cancellationToken);
            currentLegacyOrder.LegacyPaymentCode = legacyPayment?.LegacyPaymentCode;
            currentLegacyOrder.LegacyPaymentDescription = legacyPayment?.LegacyPaymentDescription;
        }

        var importRecord = await _legacyImportRecordRepository.GetBySourceDocumentAsync(''',
    '''        if (!string.Equals(applyHash, preview.CurrentSourceHash, StringComparison.Ordinal))
        {
            return PreviewExpiredConflict(preview, "Legacy source data changed after the preview. Refresh the preview before retrying reimport.");
        }

        var importRecord = await _legacyImportRecordRepository.GetBySourceDocumentAsync(''')

# Extend the primary schema with Vendedores.
schema_path = "src/Pineda.Facturacion.Infrastructure.LegacyRead/Readers/LegacyOrderReadSchema.cs"
replace_once(
    schema_path,
    "        ResolvedLegacyTable customers,\n        ResolvedLegacyTable orderItems,",
    "        ResolvedLegacyTable customers,\n        ResolvedLegacyTable vendors,\n        ResolvedLegacyTable orderItems,")
replace_once(
    schema_path,
    "        Customers = customers;\n        OrderItems = orderItems;",
    "        Customers = customers;\n        Vendors = vendors;\n        OrderItems = orderItems;")
replace_once(
    schema_path,
    "    public ResolvedLegacyTable Customers { get; }\n\n    public ResolvedLegacyTable OrderItems { get; }",
    "    public ResolvedLegacyTable Customers { get; }\n\n    public ResolvedLegacyTable Vendors { get; }\n\n    public ResolvedLegacyTable OrderItems { get; }")

reader_path = "src/Pineda.Facturacion.Infrastructure.LegacyRead/Readers/LegacyOrderReader.cs"
replace_once(
    reader_path,
    "            PaymentCondition = GetRequiredString(reader, \"PaymentCondition\"),\n            PriceListCode = GetNullableString(reader, \"PriceListCode\"),",
    "            PaymentCondition = GetRequiredString(reader, \"PaymentCondition\"),\n            LegacyPaymentCode = GetNullableString(reader, \"LegacyPaymentCode\"),\n            LegacyPaymentDescription = GetNullableString(reader, \"LegacyPaymentDescription\"),\n            PriceListCode = GetNullableString(reader, \"PriceListCode\"),")
replace_once(
    reader_path,
    '''            "pedidos",
            ["noPedido", "refPedido", "TipoPedido", "TipoDocPedido", "noCliente", "condPagoPedido", "TipoEntrega", "MontoPedido"],
            cancellationToken);
        var customers = await ResolveTableAsync(''',
    '''            "pedidos",
            ["noPedido", "refPedido", "TipoPedido", "TipoDocPedido", "noCliente", "condPagoPedido", "TipoEntrega", "MontoPedido", "cveVendedor"],
            cancellationToken);
        var customers = await ResolveTableAsync(''')
replace_once(
    reader_path,
    '''        var orderItems = await ResolveTableAsync(
            connection,
            "pedidosdet",''',
    '''        var vendors = await ResolveTableAsync(
            connection,
            "vendedores",
            ["cveVendedor", "Vendedor"],
            cancellationToken);
        var orderItems = await ResolveTableAsync(
            connection,
            "pedidosdet",''')
replace_once(
    reader_path,
    "        _schema = new LegacyOrderReadSchema(orders, customers, orderItems, articles, articleNames, invoices, salesNotes, orderDateColumn);",
    "        _schema = new LegacyOrderReadSchema(orders, customers, vendors, orderItems, articles, articleNames, invoices, salesNotes, orderDateColumn);")
replace_once(
    reader_path,
    '''                c.{Q(schema.Customers["RFC"])} AS CustomerRfc,
                p.{Q(schema.Orders["condPagoPedido"])} AS PaymentCondition,
                c.{Q(schema.Customers["TipoCliente"])} AS PriceListCode,''',
    '''                c.{Q(schema.Customers["RFC"])} AS CustomerRfc,
                p.{Q(schema.Orders["condPagoPedido"])} AS PaymentCondition,
                NULLIF(TRIM(p.{Q(schema.Orders["cveVendedor"])}), '') AS LegacyPaymentCode,
                NULLIF(TRIM(v.{Q(schema.Vendors["Vendedor"])}), '') AS LegacyPaymentDescription,
                c.{Q(schema.Customers["TipoCliente"])} AS PriceListCode,''')
replace_once(
    reader_path,
    '''            INNER JOIN {Q(schema.Customers.ActualName)} c
                ON p.{Q(schema.Orders["noCliente"])} = c.{Q(schema.Customers["noCliente"])}
            WHERE p.{Q(schema.Orders["noPedido"])} = @legacyOrderId''',
    '''            INNER JOIN {Q(schema.Customers.ActualName)} c
                ON p.{Q(schema.Orders["noCliente"])} = c.{Q(schema.Customers["noCliente"])}
            LEFT JOIN {Q(schema.Vendors.ActualName)} v
                ON v.{Q(schema.Vendors["cveVendedor"])} = p.{Q(schema.Orders["cveVendedor"])}
            WHERE p.{Q(schema.Orders["noPedido"])} = @legacyOrderId''')

# Update the schema test factory and add focused SQL assertions.
test_path = "tests/Pineda.Facturacion.UnitTests/LegacyOrderReaderTests.cs"
replace_once(
    test_path,
    '''                ("noCliente", "noCliente"),
                ("condPagoPedido", "condPagoPedido"),
                ("TipoEntrega", "TipoEntrega"),''',
    '''                ("noCliente", "noCliente"),
                ("condPagoPedido", "condPagoPedido"),
                ("cveVendedor", "cveVendedor"),
                ("TipoEntrega", "TipoEntrega"),''')
replace_once(
    test_path,
    '''            CreateTable(
                "pedidosdet",
                orderItemsTableName,''',
    '''            CreateTable(
                "vendedores",
                "Vendedores",
                ("cveVendedor", "cveVendedor"),
                ("Vendedor", "Vendedor")),
            CreateTable(
                "pedidosdet",
                orderItemsTableName,''')
insert_marker = '''    [Fact]
    public void BuildDetailSql_Uses_Resolved_Table_Names_And_Effective_Price()'''
insert_test = '''    [Fact]
    public void BuildHeaderSql_Joins_Vendedores_For_Legacy_Payment_Evidence()
    {
        var schema = CreateResolvedSchema(
            ordersTableName: "Pedidos",
            customersTableName: "Clientes",
            orderItemsTableName: "PedidosDet",
            articlesTableName: "Articulos",
            articleNamesTableName: "NombresArticulos",
            orderDateColumnName: "FechaPedido");

        var sql = LegacyOrderReader.BuildHeaderSql(schema);

        Assert.Contains("LEFT JOIN `Vendedores` v", sql, StringComparison.Ordinal);
        Assert.Contains("v.`cveVendedor` = p.`cveVendedor`", sql, StringComparison.Ordinal);
        Assert.Contains("AS LegacyPaymentCode", sql, StringComparison.Ordinal);
        Assert.Contains("AS LegacyPaymentDescription", sql, StringComparison.Ordinal);
    }

'''
replace_once(test_path, insert_marker, insert_test + insert_marker)

print("Applied post-patch isolation and primary LegacyOrderReader enrichment.")
