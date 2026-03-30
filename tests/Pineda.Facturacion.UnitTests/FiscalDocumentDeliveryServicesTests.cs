using System.Net.Mail;
using Pineda.Facturacion.Application.Abstractions.Communication;
using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Application.Abstractions.FiscalReceivers;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Storage;
using Pineda.Facturacion.Application.UseCases.FiscalDocuments;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;
using Pineda.Facturacion.Infrastructure.Documents;

namespace Pineda.Facturacion.UnitTests;

public class FiscalDocumentDeliveryServicesTests
{
    [Fact]
    public async Task GetFiscalDocumentPdf_Returns_Pdf_For_Stamped_Document()
    {
        var service = new GetFiscalDocumentPdfService(
            new FakeFiscalDocumentRepository { Existing = CreateFiscalDocument() },
            new FakeFiscalStampRepository { Existing = CreateFiscalStamp() },
            new FakeFiscalDocumentPdfRenderer());

        var result = await service.ExecuteAsync(8);

        Assert.Equal(GetFiscalDocumentPdfOutcome.Found, result.Outcome);
        Assert.Equal("AAA010101AAA_A8_BBB010101BBB.pdf", result.FileName);
        Assert.Equal("%PDF-test"u8.ToArray(), result.Content);
    }

    [Fact]
    public async Task GetFiscalDocumentPdf_Fails_When_Document_Is_Not_Stamped()
    {
        var service = new GetFiscalDocumentPdfService(
            new FakeFiscalDocumentRepository { Existing = CreateFiscalDocument() },
            new FakeFiscalStampRepository { Existing = CreateFiscalStamp(status: FiscalStampStatus.Rejected, xmlContent: null, uuid: null) },
            new FakeFiscalDocumentPdfRenderer());

        var result = await service.ExecuteAsync(8);

        Assert.Equal(GetFiscalDocumentPdfOutcome.NotStamped, result.Outcome);
    }

    [Fact]
    public async Task GetFiscalDocumentEmailDraft_Preloads_Receiver_Email()
    {
        var service = new GetFiscalDocumentEmailDraftService(
            new FakeFiscalDocumentRepository { Existing = CreateFiscalDocument() },
            new FakeFiscalStampRepository { Existing = CreateFiscalStamp() },
            new FakeFiscalReceiverRepository { Existing = new FiscalReceiver { Id = 3, Email = "cliente@example.com" } });

        var result = await service.ExecuteAsync(8);

        Assert.Equal(GetFiscalDocumentEmailDraftOutcome.Found, result.Outcome);
        Assert.Equal("cliente@example.com", result.DefaultRecipientEmail);
        Assert.Contains("A8", result.SuggestedSubject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendFiscalDocumentEmail_Attaches_Xml_And_Pdf()
    {
        var emailSender = new FakeEmailSender();
        var service = new SendFiscalDocumentEmailService(
            new FakeFiscalDocumentRepository { Existing = CreateFiscalDocument() },
            new FakeFiscalStampRepository { Existing = CreateFiscalStamp() },
            emailSender,
            new FakeFiscalDocumentPdfRenderer());

        var result = await service.ExecuteAsync(
            new SendFiscalDocumentEmailCommand
            {
                FiscalDocumentId = 8,
                Recipients = ["cliente@example.com"]
            });

        Assert.Equal(SendFiscalDocumentEmailOutcome.Sent, result.Outcome);
        Assert.NotNull(emailSender.LastMessage);
        Assert.Equal(["cliente@example.com"], emailSender.LastMessage!.Recipients);
        Assert.Equal(2, emailSender.LastMessage.Attachments.Count);
        Assert.Contains(emailSender.LastMessage.Attachments, attachment => attachment.ContentType == "application/xml" && attachment.FileName == "AAA010101AAA_A8_BBB010101BBB.xml");
        Assert.Contains(emailSender.LastMessage.Attachments, attachment => attachment.ContentType == "application/pdf" && attachment.FileName == "AAA010101AAA_A8_BBB010101BBB.pdf");
    }

    [Fact]
    public async Task SendFiscalDocumentEmail_Does_Not_Change_Stamping_State_When_Email_Fails()
    {
        var fiscalStamp = CreateFiscalStamp();
        var service = new SendFiscalDocumentEmailService(
            new FakeFiscalDocumentRepository { Existing = CreateFiscalDocument() },
            new FakeFiscalStampRepository { Existing = fiscalStamp },
            new FakeEmailSender { Exception = new SmtpException("SMTP down") },
            new FakeFiscalDocumentPdfRenderer());

        var result = await service.ExecuteAsync(
            new SendFiscalDocumentEmailCommand
            {
                FiscalDocumentId = 8,
                Recipients = ["cliente@example.com"]
            });

        Assert.Equal(SendFiscalDocumentEmailOutcome.DeliveryFailed, result.Outcome);
        Assert.Equal(FiscalStampStatus.Succeeded, fiscalStamp.Status);
    }

    [Fact]
    public async Task FiscalDocumentPdfRenderer_Generates_A_Real_Pdf_From_Stamped_Xml()
    {
        var renderer = new FiscalDocumentPdfRenderer(new FakeIssuerProfileRepository(), new FakeIssuerProfileLogoStorage(), new SatCatalogDescriptionProvider(new FakeFiscalReceiverSatCatalogProvider()));
        var bytes = await renderer.RenderAsync(CreateFiscalDocument(), CreateFiscalStamp());
        var pdfText = System.Text.Encoding.ASCII.GetString(bytes);

        Assert.StartsWith("%PDF-1.4", pdfText, StringComparison.Ordinal);
        Assert.Contains("FISCALDOM", pdfText, StringComparison.Ordinal);
        Assert.Contains("01000", pdfText, StringComparison.Ordinal);
        Assert.Contains("601 - General de Ley Personas Morales", pdfText, StringComparison.Ordinal);
        Assert.Contains("G03 - Gastos en general", pdfText, StringComparison.Ordinal);
        Assert.Contains("03 - Transferencia electronica de fondos", pdfText, StringComparison.Ordinal);
        Assert.Contains("PUE - Pago en una sola exhibicion", pdfText, StringComparison.Ordinal);
        Assert.Contains("01 - No aplica", pdfText, StringComparison.Ordinal);
        Assert.Contains("Domicilio Fiscal:", pdfText, StringComparison.Ordinal);
        Assert.Contains("Regimen fiscal:", pdfText, StringComparison.Ordinal);
        Assert.Contains("Uso CFDI:", pdfText, StringComparison.Ordinal);
        Assert.Contains("Moneda:", pdfText, StringComparison.Ordinal);
        Assert.DoesNotContain("Domicilio fiscal receptor:", pdfText, StringComparison.Ordinal);
        Assert.DoesNotContain("Regimen fiscal receptor:", pdfText, StringComparison.Ordinal);
        Assert.Contains("CIENTO DIECISEIS PESOS 00/100 M.N.", pdfText, StringComparison.Ordinal);
        Assert.Contains("100.00", pdfText, StringComparison.Ordinal);
        Assert.Contains("16.00", pdfText, StringComparison.Ordinal);
        Assert.Contains("116.00", pdfText, StringComparison.Ordinal);
        Assert.Contains("Este documento es una representacion impresa de un CFDI 4.0", pdfText, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(pdfText, "/Subtype /Image"));
        Assert.Contains("CONSULTA SAT / QR:", pdfText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FiscalDocumentPdfRenderer_Falls_Back_When_Logo_Is_Not_Available()
    {
        var renderer = new FiscalDocumentPdfRenderer(
            new FakeIssuerProfileRepository { Existing = new IssuerProfile { Id = 1, LogoStoragePath = "missing/logo.png" } },
            new FakeIssuerProfileLogoStorage(),
            new SatCatalogDescriptionProvider(new FakeFiscalReceiverSatCatalogProvider()));

        var bytes = await renderer.RenderAsync(CreateFiscalDocument(), CreateFiscalStamp());
        var pdfText = System.Text.Encoding.ASCII.GetString(bytes);

        Assert.StartsWith("%PDF-1.4", pdfText, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(pdfText, "/Subtype /Image"));
        Assert.Contains("CFDI", pdfText, StringComparison.Ordinal);
        Assert.Contains("FISCALDOM", pdfText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FiscalDocumentPdfRenderer_Renders_Additional_Special_Fields_When_Present()
    {
        var fiscalDocument = CreateFiscalDocument();
        fiscalDocument.SpecialFieldValues =
        [
            new FiscalDocumentSpecialFieldValue
            {
                Id = 1,
                FiscalDocumentId = fiscalDocument.Id,
                FieldCode = "AGENTE",
                FieldLabelSnapshot = "Agente",
                DataType = "text",
                Value = "Juan Perez",
                DisplayOrder = 1,
                CreatedAtUtc = new DateTime(2026, 3, 24, 12, 0, 0, DateTimeKind.Utc)
            },
            new FiscalDocumentSpecialFieldValue
            {
                Id = 2,
                FiscalDocumentId = fiscalDocument.Id,
                FieldCode = "ORDEN_TRABAJO",
                FieldLabelSnapshot = "Orden de trabajo",
                DataType = "text",
                Value = "OT-45678",
                DisplayOrder = 2,
                CreatedAtUtc = new DateTime(2026, 3, 24, 12, 0, 0, DateTimeKind.Utc)
            }
        ];

        var renderer = new FiscalDocumentPdfRenderer(new FakeIssuerProfileRepository(), new FakeIssuerProfileLogoStorage(), new SatCatalogDescriptionProvider(new FakeFiscalReceiverSatCatalogProvider()));
        var bytes = await renderer.RenderAsync(fiscalDocument, CreateFiscalStamp());
        var pdfText = System.Text.Encoding.ASCII.GetString(bytes);

        Assert.Contains("Datos adicionales", pdfText, StringComparison.Ordinal);
        Assert.Contains("Agente", pdfText, StringComparison.Ordinal);
        Assert.Contains("Juan Perez", pdfText, StringComparison.Ordinal);
        Assert.Contains("Orden de trabajo", pdfText, StringComparison.Ordinal);
        Assert.Contains("OT-45678", pdfText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FiscalDocumentPdfRenderer_Still_Generates_When_Qr_Cannot_Be_Built()
    {
        var renderer = new FiscalDocumentPdfRenderer(
            new FakeIssuerProfileRepository { Existing = new IssuerProfile { Id = 1, LogoStoragePath = "missing/logo.png" } },
            new FakeIssuerProfileLogoStorage(),
            new SatCatalogDescriptionProvider(new FakeFiscalReceiverSatCatalogProvider()));

        var bytes = await renderer.RenderAsync(
            CreateFiscalDocument(),
            CreateFiscalStamp(
                xmlContent: """
                    <?xml version="1.0" encoding="utf-8"?>
                    <cfdi:Comprobante xmlns:cfdi="http://www.sat.gob.mx/cfd/4" xmlns:tfd="http://www.sat.gob.mx/TimbreFiscalDigital" Version="4.0" Serie="A" Folio="8" Fecha="2026-03-24T06:00:00" SubTotal="100.00" Total="116.00" Moneda="MXN" MetodoPago="PUE" FormaPago="03" LugarExpedicion="01000">
                      <cfdi:Emisor Rfc="AAA010101AAA" Nombre="Emisor SA" RegimenFiscal="601" />
                      <cfdi:Receptor Rfc="BBB010101BBB" Nombre="Cliente SA" UsoCFDI="G03" RegimenFiscalReceptor="601" DomicilioFiscalReceptor="02000" />
                      <cfdi:Conceptos>
                        <cfdi:Concepto ClaveProdServ="01010101" Cantidad="1" ClaveUnidad="H87" Descripcion="Producto" ValorUnitario="100.00" Importe="100.00" ObjetoImp="02" />
                      </cfdi:Conceptos>
                      <cfdi:Complemento>
                        <tfd:TimbreFiscalDigital UUID="4cb4eed3-3d93-4938-8872-028106881e4c" FechaTimbrado="2026-03-24T06:05:59" NoCertificadoSAT="00001000000500001234" Version="1.1" />
                      </cfdi:Complemento>
                    </cfdi:Comprobante>
                    """,
                uuid: "4cb4eed3-3d93-4938-8872-028106881e4c",
                qrCodeTextOrUrl: null));

        var pdfText = System.Text.Encoding.ASCII.GetString(bytes);

        Assert.StartsWith("%PDF-1.4", pdfText, StringComparison.Ordinal);
        Assert.Equal(0, CountOccurrences(pdfText, "/Subtype /Image"));
        Assert.DoesNotContain("CONSULTA SAT / QR:", pdfText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FiscalDocumentPdfRenderer_Renders_Multiple_Concepts_In_Table()
    {
        var renderer = new FiscalDocumentPdfRenderer(new FakeIssuerProfileRepository(), new FakeIssuerProfileLogoStorage(), new SatCatalogDescriptionProvider(new FakeFiscalReceiverSatCatalogProvider()));
        var bytes = await renderer.RenderAsync(
            CreateFiscalDocument(),
            CreateFiscalStamp(
                xmlContent: """
                    <?xml version="1.0" encoding="utf-8"?>
                    <cfdi:Comprobante xmlns:cfdi="http://www.sat.gob.mx/cfd/4" xmlns:tfd="http://www.sat.gob.mx/TimbreFiscalDigital" Version="4.0" Serie="A" Folio="8" Fecha="2026-03-24T06:00:00" SubTotal="100.00" Total="116.00" Moneda="MXN" MetodoPago="PUE" FormaPago="03" LugarExpedicion="01000" Sello="SE1234567890ABCDEF1234567890ABCDEF" NoCertificado="30001000000500003416">
                      <cfdi:Emisor Rfc="AAA010101AAA" Nombre="Emisor SA" RegimenFiscal="601" />
                      <cfdi:Receptor Rfc="BBB010101BBB" Nombre="Cliente SA" UsoCFDI="G03" RegimenFiscalReceptor="601" DomicilioFiscalReceptor="02000" />
                      <cfdi:Conceptos>
                        <cfdi:Concepto ClaveProdServ="01010101" NoIdentificacion="SKU-1" Cantidad="1" ClaveUnidad="H87" Unidad="PIEZA" Descripcion="Producto uno muy importante" ValorUnitario="40.00" Importe="40.00" ObjetoImp="02" />
                        <cfdi:Concepto ClaveProdServ="01010102" NoIdentificacion="SKU-2" Cantidad="2" ClaveUnidad="H87" Unidad="PIEZA" Descripcion="Producto dos de prueba" ValorUnitario="30.00" Importe="60.00" ObjetoImp="02" />
                      </cfdi:Conceptos>
                      <cfdi:Impuestos TotalImpuestosTrasladados="16.00">
                        <cfdi:Traslados>
                          <cfdi:Traslado Base="100.00" Impuesto="002" TipoFactor="Tasa" TasaOCuota="0.160000" Importe="16.00" />
                        </cfdi:Traslados>
                      </cfdi:Impuestos>
                      <cfdi:Complemento>
                        <tfd:TimbreFiscalDigital UUID="4cb4eed3-3d93-4938-8872-028106881e4c" FechaTimbrado="2026-03-24T06:05:59" NoCertificadoSAT="00001000000500001234" SelloSAT="SELLOSAT1234567890" Version="1.1" />
                      </cfdi:Complemento>
                    </cfdi:Comprobante>
                    """));

        var pdfText = System.Text.Encoding.ASCII.GetString(bytes);

        Assert.Contains("Producto uno muy importante", pdfText, StringComparison.Ordinal);
        Assert.Contains("Producto dos de prueba", pdfText, StringComparison.Ordinal);
        Assert.Contains("SKU-1", pdfText, StringComparison.Ordinal);
        Assert.Contains("SKU-2", pdfText, StringComparison.Ordinal);
        Assert.Contains("002 Tasa 0.160000: 16.00", pdfText, StringComparison.Ordinal);
        Assert.Contains("40.00", pdfText, StringComparison.Ordinal);
        Assert.Contains("60.00", pdfText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FiscalDocumentPdfRenderer_Wraps_Long_Fiscal_And_Timbre_Text_Without_Losing_Content()
    {
        var renderer = new FiscalDocumentPdfRenderer(new FakeIssuerProfileRepository(), new FakeIssuerProfileLogoStorage(), new SatCatalogDescriptionProvider(new FakeFiscalReceiverSatCatalogProvider()));
        var bytes = await renderer.RenderAsync(
            CreateFiscalDocument(),
            CreateFiscalStamp(
                xmlContent: """
                    <?xml version="1.0" encoding="utf-8"?>
                    <cfdi:Comprobante xmlns:cfdi="http://www.sat.gob.mx/cfd/4" xmlns:tfd="http://www.sat.gob.mx/TimbreFiscalDigital" Version="4.0" Serie="SERIE-LARGA" Folio="FOLIO-EXCESIVAMENTE-LARGO-2026-00000008" Fecha="2026-03-24T06:00:00" SubTotal="75" Total="282" Moneda="MXN" MetodoPago="PPD-CON-REFERENCIA-MUY-LARGA" FormaPago="03-TARJETA" LugarExpedicion="01000" Sello="ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890" NoCertificado="30001000000500003416">
                      <cfdi:Emisor Rfc="AAA010101AAA" Nombre="Emisor SA" RegimenFiscal="601" />
                      <cfdi:Receptor Rfc="BBB010101BBB" Nombre="Cliente SA" UsoCFDI="G03" RegimenFiscalReceptor="601" DomicilioFiscalReceptor="02000" />
                      <cfdi:Conceptos>
                        <cfdi:Concepto ClaveProdServ="01010101" NoIdentificacion="SKU-1" Cantidad="1" ClaveUnidad="H87" Unidad="PIEZA" Descripcion="Producto de prueba con descripcion extensa" ValorUnitario="75" Importe="75" ObjetoImp="02" />
                      </cfdi:Conceptos>
                      <cfdi:Impuestos TotalImpuestosTrasladados="35" />
                      <cfdi:Complemento>
                        <tfd:TimbreFiscalDigital UUID="4cb4eed3-3d93-4938-8872-028106881e4c-EXTRA-LARGO-PARA-PRUEBA-DE-WRAP" FechaTimbrado="2026-03-24T06:05:59" NoCertificadoSAT="00001000000500001234-EXTRA-LARGO" SelloSAT="SELLOSAT1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890" Version="1.1" />
                      </cfdi:Complemento>
                    </cfdi:Comprobante>
                    """,
                qrCodeTextOrUrl: "https://verificacfdi.facturaelectronica.sat.gob.mx/default.aspx?id=4cb4eed3-3d93-4938-8872-028106881e4c-EXTRA-LARGO-PARA-PRUEBA-DE-WRAP&re=AAA010101AAA&rr=BBB010101BBB&tt=282.00&fe=12345678"));

        var pdfText = System.Text.Encoding.ASCII.GetString(bytes);

        Assert.Contains("SELLOSAT1234567890", pdfText, StringComparison.Ordinal);
        Assert.Contains("ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890", pdfText, StringComparison.Ordinal);
        Assert.Contains("75.00", pdfText, StringComparison.Ordinal);
        Assert.Contains("282.00", pdfText, StringComparison.Ordinal);
        Assert.Contains("35.00", pdfText, StringComparison.Ordinal);
        Assert.Contains("Este documento es una representacion impresa de un CFDI 4.0", pdfText, StringComparison.Ordinal);
    }

    [Fact]
    public void FitTextToWidth_Breaks_Long_Text_By_Real_Width()
    {
        var first = PdfLayoutTextWrapper.FitTextToWidth(
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ 1234567890 QWERTYUIOP ASDFGHJKL ZXCVBNM",
            80f,
            6.2f);

        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.NotEqual("ABCDEFGHIJKLMNOPQRSTUVWXYZ 1234567890 QWERTYUIOP ASDFGHJKL ZXCVBNM", first);
    }

    [Fact]
    public void FitTextToWidth_Splits_Long_Token_Without_Spaces()
    {
        var chunk = PdfLayoutTextWrapper.FitTextToWidth(
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890",
            30f,
            6.2f);

        Assert.False(string.IsNullOrWhiteSpace(chunk));
        Assert.True(chunk.Length < "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".Length);
    }

    [Fact]
    public void WrapValueWithHangingLabel_Uses_First_Line_After_Label_And_Fuller_Following_Lines()
    {
        var lines = PdfLayoutTextWrapper.WrapValueWithHangingLabel(
            "SELLO DIGITAL DEL SAT",
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ 1234567890 QWERTYUIOP ASDFGHJKL ZXCVBNM",
            180f,
            8.5f,
            6.2f);

        Assert.True(lines.Length >= 2);
        Assert.True(lines[1].Length >= lines[0].Length);
    }

    [Fact]
    public async Task FiscalDocumentPdfRenderer_Uses_Uniform_Uppercase_Timbre_Labels()
    {
        var renderer = new FiscalDocumentPdfRenderer(new FakeIssuerProfileRepository(), new FakeIssuerProfileLogoStorage(), new SatCatalogDescriptionProvider(new FakeFiscalReceiverSatCatalogProvider()));
        var bytes = await renderer.RenderAsync(CreateFiscalDocument(), CreateFiscalStamp());

        var pdfText = System.Text.Encoding.ASCII.GetString(bytes);

        Assert.Contains("NO. SERIE CERTIFICADO SAT:", pdfText, StringComparison.Ordinal);
        Assert.Contains("SELLO DIGITAL DEL CFDI:", pdfText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FiscalDocumentPdfRenderer_Falls_Back_To_Code_When_Sat_Description_Is_Missing()
    {
        var renderer = new FiscalDocumentPdfRenderer(new FakeIssuerProfileRepository(), new FakeIssuerProfileLogoStorage(), new SatCatalogDescriptionProvider(new FakeFiscalReceiverSatCatalogProvider()));
        var fiscalDocument = CreateFiscalDocument();
        fiscalDocument.IssuerFiscalRegimeCode = "999";
        fiscalDocument.ReceiverFiscalRegimeCode = "998";
        fiscalDocument.ReceiverCfdiUseCode = "ZZZ";
        fiscalDocument.PaymentFormSat = "77";
        fiscalDocument.PaymentMethodSat = "ABC";

        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <cfdi:Comprobante xmlns:cfdi="http://www.sat.gob.mx/cfd/4" xmlns:tfd="http://www.sat.gob.mx/TimbreFiscalDigital" Version="4.0" Serie="A" Folio="8" Fecha="2026-03-24T06:00:00" SubTotal="100.00" Total="116.00" Moneda="MXN" MetodoPago="ABC" FormaPago="77" Exportacion="88" LugarExpedicion="01000" Sello="SE1234567890ABCDEF1234567890ABCDEF">
              <cfdi:Emisor Rfc="AAA010101AAA" Nombre="Emisor SA" RegimenFiscal="999" />
              <cfdi:Receptor Rfc="BBB010101BBB" Nombre="Cliente SA" UsoCFDI="ZZZ" RegimenFiscalReceptor="998" DomicilioFiscalReceptor="02000" />
              <cfdi:Conceptos>
                <cfdi:Concepto ClaveProdServ="01010101" Cantidad="1" ClaveUnidad="H87" Descripcion="Producto" ValorUnitario="100.00" Importe="100.00" ObjetoImp="02" />
              </cfdi:Conceptos>
              <cfdi:Impuestos TotalImpuestosTrasladados="16.00" />
              <cfdi:Complemento>
                <tfd:TimbreFiscalDigital UUID="4cb4eed3-3d93-4938-8872-028106881e4c" FechaTimbrado="2026-03-24T06:05:59" NoCertificadoSAT="00001000000500001234" Version="1.1" />
              </cfdi:Complemento>
            </cfdi:Comprobante>
            """;

        var bytes = await renderer.RenderAsync(fiscalDocument, CreateFiscalStamp(xmlContent: xml));
        var pdfText = System.Text.Encoding.ASCII.GetString(bytes);

        Assert.Contains("999", pdfText, StringComparison.Ordinal);
        Assert.DoesNotContain("999 -", pdfText, StringComparison.Ordinal);
        Assert.Contains("998", pdfText, StringComparison.Ordinal);
        Assert.DoesNotContain("998 -", pdfText, StringComparison.Ordinal);
        Assert.Contains("ZZZ", pdfText, StringComparison.Ordinal);
        Assert.DoesNotContain("ZZZ -", pdfText, StringComparison.Ordinal);
        Assert.Contains("77", pdfText, StringComparison.Ordinal);
        Assert.DoesNotContain("77 -", pdfText, StringComparison.Ordinal);
        Assert.Contains("ABC", pdfText, StringComparison.Ordinal);
        Assert.DoesNotContain("ABC -", pdfText, StringComparison.Ordinal);
        Assert.Contains("88", pdfText, StringComparison.Ordinal);
        Assert.DoesNotContain("88 -", pdfText, StringComparison.Ordinal);
    }

    private static FiscalDocument CreateFiscalDocument()
    {
        return new FiscalDocument
        {
            Id = 8,
            BillingDocumentId = 3,
            IssuerProfileId = 1,
            FiscalReceiverId = 3,
            Status = FiscalDocumentStatus.Stamped,
            Series = "A",
            Folio = "8",
            IssuedAtUtc = new DateTime(2026, 3, 24, 12, 0, 0, DateTimeKind.Utc),
            CurrencyCode = "MXN",
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            IssuerRfc = "AAA010101AAA",
            IssuerLegalName = "Emisor SA",
            IssuerFiscalRegimeCode = "601",
            IssuerPostalCode = "01000",
            ReceiverRfc = "BBB010101BBB",
            ReceiverLegalName = "Cliente SA",
            ReceiverFiscalRegimeCode = "601",
            ReceiverCfdiUseCode = "G03",
            ReceiverPostalCode = "02000",
            Subtotal = 100m,
            DiscountTotal = 0m,
            TaxTotal = 16m,
            Total = 116m
        };
    }

    private static FiscalStamp CreateFiscalStamp(
        FiscalStampStatus status = FiscalStampStatus.Succeeded,
        string? xmlContent = """
            <?xml version="1.0" encoding="utf-8"?>
            <cfdi:Comprobante xmlns:cfdi="http://www.sat.gob.mx/cfd/4" xmlns:tfd="http://www.sat.gob.mx/TimbreFiscalDigital" Version="4.0" Serie="A" Folio="8" Fecha="2026-03-24T06:00:00" SubTotal="100.00" Total="116.00" Moneda="MXN" MetodoPago="PUE" FormaPago="03" LugarExpedicion="01000" Sello="SE1234567890ABCDEF1234567890ABCDEF">
              <cfdi:Emisor Rfc="AAA010101AAA" Nombre="Emisor SA" RegimenFiscal="601" />
              <cfdi:Receptor Rfc="BBB010101BBB" Nombre="Cliente SA" UsoCFDI="G03" RegimenFiscalReceptor="601" DomicilioFiscalReceptor="02000" />
              <cfdi:Conceptos>
                <cfdi:Concepto ClaveProdServ="01010101" Cantidad="1" ClaveUnidad="H87" Descripcion="Producto" ValorUnitario="100.00" Importe="100.00" ObjetoImp="02" />
              </cfdi:Conceptos>
              <cfdi:Impuestos TotalImpuestosTrasladados="16.00" />
              <cfdi:Complemento>
                <tfd:TimbreFiscalDigital UUID="4cb4eed3-3d93-4938-8872-028106881e4c" FechaTimbrado="2026-03-24T06:05:59" NoCertificadoSAT="00001000000500001234" Version="1.1" />
              </cfdi:Complemento>
            </cfdi:Comprobante>
            """,
        string? uuid = "4cb4eed3-3d93-4938-8872-028106881e4c",
        string? qrCodeTextOrUrl = "https://sat.example/qr")
    {
        return new FiscalStamp
        {
            Id = 8,
            FiscalDocumentId = 8,
            Status = status,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "stamp",
            Uuid = uuid,
            StampedAtUtc = new DateTime(2026, 3, 24, 12, 5, 59, DateTimeKind.Utc),
            XmlContent = xmlContent,
            QrCodeTextOrUrl = qrCodeTextOrUrl
        };
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private sealed class FakeFiscalDocumentRepository : IFiscalDocumentRepository
    {
        public FiscalDocument? Existing { get; set; }

        public Task<FiscalDocument?> GetByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default) => Task.FromResult(Existing);
        public Task<FiscalDocument?> GetTrackedByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default) => Task.FromResult(Existing);
        public Task<FiscalDocument?> GetByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default) => Task.FromResult(Existing);
        public Task<bool> ExistsByIssuerSeriesAndFolioAsync(string issuerRfc, string series, string folio, long? excludeFiscalDocumentId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<int?> GetLastUsedFolioAsync(string issuerRfc, string series, CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);
        public Task AddAsync(FiscalDocument fiscalDocument, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeFiscalStampRepository : IFiscalStampRepository
    {
        public FiscalStamp? Existing { get; set; }

        public Task<FiscalStamp?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default) => Task.FromResult(Existing);
        public Task<FiscalStamp?> GetTrackedByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default) => Task.FromResult(Existing);
        public Task<FiscalStamp?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default) => Task.FromResult(Existing);
        public Task<FiscalStamp?> GetTrackedByUuidAsync(string uuid, CancellationToken cancellationToken = default) => Task.FromResult(Existing);
        public Task AddAsync(FiscalStamp fiscalStamp, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeFiscalReceiverRepository : IFiscalReceiverRepository
    {
        public FiscalReceiver? Existing { get; set; }

        public Task<IReadOnlyList<FiscalReceiver>> SearchAsync(string query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FiscalReceiver>>([]);
        public Task<FiscalReceiver?> GetByRfcAsync(string normalizedRfc, CancellationToken cancellationToken = default) => Task.FromResult(Existing);
        public Task<FiscalReceiver?> GetByIdAsync(long fiscalReceiverId, CancellationToken cancellationToken = default) => Task.FromResult(Existing);
        public Task<IReadOnlyList<FiscalReceiverSpecialFieldDefinition>> GetActiveSpecialFieldDefinitionsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FiscalReceiverSpecialFieldDefinition>>([]);
        public Task AddAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeFiscalDocumentPdfRenderer : IFiscalDocumentPdfRenderer
    {
        public Task<byte[]> RenderAsync(FiscalDocument fiscalDocument, FiscalStamp fiscalStamp, CancellationToken cancellationToken = default)
            => Task.FromResult("%PDF-test"u8.ToArray());
    }

    private sealed class FakeIssuerProfileRepository : IIssuerProfileRepository
    {
        public IssuerProfile? Existing { get; set; } = new()
        {
            Id = 1,
            LogoStoragePath = "issuer/1/logo.png",
            LogoContentType = "image/png"
        };

        public Task<IssuerProfile?> GetActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult(Existing);
        public Task<IssuerProfile?> GetTrackedActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult(Existing);
        public Task<IssuerProfile?> GetByIdAsync(long issuerProfileId, CancellationToken cancellationToken = default) => Task.FromResult(Existing?.Id == issuerProfileId ? Existing : null);
        public Task<bool> TryAdvanceNextFiscalFolioAsync(long issuerProfileId, int expectedNextFiscalFolio, int newNextFiscalFolio, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeFiscalReceiverSatCatalogProvider : IFiscalReceiverSatCatalogProvider
    {
        private readonly FiscalReceiverSatCatalog _catalog = new()
        {
            RegimenFiscal =
            [
                new FiscalReceiverSatCatalogOption { Code = "601", Description = "General de Ley Personas Morales" },
                new FiscalReceiverSatCatalogOption { Code = "612", Description = "Personas Fisicas con Actividades Empresariales y Profesionales" }
            ],
            UsoCfdi =
            [
                new FiscalReceiverSatCatalogOption { Code = "G01", Description = "Adquisicion de mercancias" },
                new FiscalReceiverSatCatalogOption { Code = "G03", Description = "Gastos en general" }
            ],
            ByRegimenFiscal =
            [
                new FiscalReceiverSatRegimeCompatibility
                {
                    Code = "601",
                    Description = "General de Ley Personas Morales",
                    AllowedUsoCfdi = [new FiscalReceiverSatCatalogOption { Code = "G03", Description = "Gastos en general" }]
                }
            ]
        };

        public FiscalReceiverSatCatalog GetCatalog() => _catalog;
        public bool FiscalRegimeExists(string code) => _catalog.RegimenFiscal.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
        public bool CfdiUseExists(string code) => _catalog.UsoCfdi.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
        public bool IsCfdiUseCompatibleWithRegime(string fiscalRegimeCode, string cfdiUseCode) => true;
    }

    private sealed class FakeIssuerProfileLogoStorage : IIssuerProfileLogoStorage
    {
        public Task<StoreIssuerProfileLogoResult> SaveAsync(long issuerProfileId, string fileName, string? declaredContentType, byte[] content, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IssuerProfileLogoBinary?> ReadAsync(string storagePath, CancellationToken cancellationToken = default)
        {
            if (storagePath.Contains("missing", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<IssuerProfileLogoBinary?>(null);
            }

            return Task.FromResult<IssuerProfileLogoBinary?>(new IssuerProfileLogoBinary
            {
                Content = SmallPng(),
                ContentType = "image/png",
                FileName = "logo.png"
            });
        }

        public Task DeleteAsync(string? storagePath, CancellationToken cancellationToken = default) => Task.CompletedTask;

        private static byte[] SmallPng()
        {
            return
            [
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
                0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
                0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
                0x89, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41,
                0x54, 0x78, 0x9C, 0x63, 0xF8, 0xCF, 0xC0, 0xF0,
                0x1F, 0x00, 0x05, 0x00, 0x01, 0xFF, 0x89, 0x99,
                0x3D, 0x1D, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45,
                0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
            ];
        }
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public EmailMessage? LastMessage { get; private set; }
        public Exception? Exception { get; init; }

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            LastMessage = message;
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.CompletedTask;
        }
    }
}
