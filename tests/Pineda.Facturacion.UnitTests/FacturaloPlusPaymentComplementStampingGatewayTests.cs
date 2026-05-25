using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Pineda.Facturacion.Application.Abstractions.Secrets;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Infrastructure.FacturaloPlus.FacturaloPlus;
using Pineda.Facturacion.Infrastructure.FacturaloPlus.Options;

namespace Pineda.Facturacion.UnitTests;

public class FacturaloPlusPaymentComplementStampingGatewayTests
{
    [Fact]
    public async Task StampAsync_Uses_TimbrarJson3_FormUrlEncoded_Request_For_Rep()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "code": "200",
                  "message": "Solicitud procesada con éxito. - Complemento timbrado.",
                  "data": {
                    "uuid": "UUID-PC-OK-1",
                    "xml": "<cfdi:Comprobante xmlns:cfdi=\"http://www.sat.gob.mx/cfd/4\" />"
                  }
                }
                """, Encoding.UTF8, "application/json")
        });

        var gateway = CreateGateway(handler);

        var result = await gateway.StampAsync(CreateRequest());

        Assert.Equal(PaymentComplementStampingGatewayOutcome.Stamped, result.Outcome);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://dev.facturaloplus.com/api/rest/servicio/timbrarJSON3", handler.LastRequest.RequestUri!.ToString());
        Assert.Equal("application/x-www-form-urlencoded", handler.LastContentType);
        Assert.False(handler.LastRequest.Headers.Contains("X-Api-Key"));
        Assert.Equal("timbrarJSON3", result.ProviderOperation);
        Assert.Contains("\"requestUri\":\"https://dev.facturaloplus.com/api/rest/servicio/timbrarJSON3\"", result.RawResponseSummaryJson, StringComparison.Ordinal);

        var form = ParseFormBody(handler.LastBody!);
        Assert.Equal("APIKEY-TEST", form["apikey"]);
        Assert.Equal("PRIVATE-KEY-PEM", form["keyPEM"]);
        Assert.True(form.ContainsKey("cerPEM"));
        Assert.True(form.ContainsKey("jsonB64"));

        var decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(form["jsonB64"]));
        using var json = JsonDocument.Parse(decodedJson);
        var comprobante = json.RootElement.GetProperty("Comprobante");
        Assert.Equal("P", comprobante.GetProperty("TipoDeComprobante").GetString());
        Assert.Equal("XXX", comprobante.GetProperty("Moneda").GetString());
        Assert.Equal(0m, comprobante.GetProperty("SubTotal").GetDecimal());
        Assert.Equal(0m, comprobante.GetProperty("Total").GetDecimal());
        Assert.Equal("01", comprobante.GetProperty("Exportacion").GetString());
        Assert.Equal("CP01", comprobante.GetProperty("Receptor").GetProperty("UsoCFDI").GetString());
        Assert.False(comprobante.TryGetProperty("FormaPago", out _));
        Assert.False(comprobante.TryGetProperty("MetodoPago", out _));
        Assert.False(comprobante.TryGetProperty("CondicionesDePago", out _));
        Assert.False(comprobante.TryGetProperty("Descuento", out _));
        Assert.False(comprobante.TryGetProperty("TipoCambio", out _));
        Assert.False(comprobante.TryGetProperty("Impuestos", out _));

        var conceptos = comprobante.GetProperty("Conceptos");
        Assert.Equal(JsonValueKind.Array, conceptos.ValueKind);
        Assert.Equal(1, conceptos.GetArrayLength());
        Assert.Equal("84111506", conceptos[0].GetProperty("ClaveProdServ").GetString());
        Assert.Equal(1m, conceptos[0].GetProperty("Cantidad").GetDecimal());
        Assert.Equal("ACT", conceptos[0].GetProperty("ClaveUnidad").GetString());
        Assert.Equal("Pago", conceptos[0].GetProperty("Descripcion").GetString());
        Assert.Equal(0m, conceptos[0].GetProperty("ValorUnitario").GetDecimal());
        Assert.Equal(0m, conceptos[0].GetProperty("Importe").GetDecimal());
        Assert.Equal("01", conceptos[0].GetProperty("ObjetoImp").GetString());

        var complemento = comprobante.GetProperty("Complemento");
        Assert.Equal(JsonValueKind.Array, complemento.ValueKind);
        Assert.True(complemento.GetArrayLength() > 0);
        Assert.True(complemento[0].TryGetProperty("Pagos20", out var pagos20));
        Assert.False(complemento[0].TryGetProperty("Pagos", out _));
        Assert.DoesNotContain("\"Pagos\":", decodedJson, StringComparison.Ordinal);
        Assert.Equal("2.0", pagos20.GetProperty("Version").GetString());
        Assert.Equal(JsonValueKind.Array, pagos20.GetProperty("Pago").ValueKind);
        Assert.Equal("03", pagos20.GetProperty("Pago")[0].GetProperty("FormaDePagoP").GetString());
        Assert.Equal("MXN", pagos20.GetProperty("Pago")[0].GetProperty("MonedaP").GetString());
        Assert.Equal("1", pagos20.GetProperty("Pago")[0].GetProperty("TipoCambioP").GetString());
        Assert.Equal(JsonValueKind.Array, pagos20.GetProperty("Pago")[0].GetProperty("DoctoRelacionado").ValueKind);
        Assert.Equal("UUID-REL-1", pagos20.GetProperty("Pago")[0].GetProperty("DoctoRelacionado")[0].GetProperty("IdDocumento").GetString());
        Assert.Equal("1", pagos20.GetProperty("Pago")[0].GetProperty("DoctoRelacionado")[0].GetProperty("EquivalenciaDR").GetString());
        Assert.Equal("Complemento de Pago", json.RootElement.GetProperty("CamposPDF").GetProperty("tipoComprobante").GetString());
        Assert.Equal("Complemento Vigente", json.RootElement.GetProperty("CamposPDF").GetProperty("Comentarios").GetString());
        Assert.Contains("\"monedaP\":\"MXN\"", result.RawResponseSummaryJson, StringComparison.Ordinal);
        Assert.Contains("\"tipoCambioP\":\"1\"", result.RawResponseSummaryJson, StringComparison.Ordinal);
        Assert.Contains("\"tipoCambioPNormalizedForMxn\":true", result.RawResponseSummaryJson, StringComparison.Ordinal);
        Assert.Contains("\"monedaDR\":\"MXN\"", result.RawResponseSummaryJson, StringComparison.Ordinal);
        Assert.Contains("\"equivalenciaDR\":\"1\"", result.RawResponseSummaryJson, StringComparison.Ordinal);
        Assert.Contains("\"equivalenciaDRNormalizedBecauseSameCurrency\":true", result.RawResponseSummaryJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StampAsync_Normalizes_TipoCambioP_To_1_For_Mxn_When_ExchangeRate_Is_Zero()
    {
        var handler = CreateSuccessfulHandler();
        var gateway = CreateGateway(handler);
        var request = CreateRequest();
        request.Payments[0].ExchangeRate = 0m;

        var result = await gateway.StampAsync(request);

        Assert.Equal(PaymentComplementStampingGatewayOutcome.Stamped, result.Outcome);

        var decodedJson = DecodePayloadJson(handler);
        using var json = JsonDocument.Parse(decodedJson);
        var tipoCambioP = json.RootElement.GetProperty("Comprobante").GetProperty("Complemento")[0].GetProperty("Pagos20").GetProperty("Pago")[0].GetProperty("TipoCambioP").GetString();
        Assert.Equal("1", tipoCambioP);
        Assert.DoesNotContain("\"TipoCambioP\":\"0\"", decodedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"TipoCambioP\":\"0.000000\"", decodedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"TipoCambioP\":0", decodedJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StampAsync_Normalizes_TipoCambioP_To_1_For_Mxn_When_ExchangeRate_Is_One()
    {
        var handler = CreateSuccessfulHandler();
        var gateway = CreateGateway(handler);
        var request = CreateRequest();
        request.Payments[0].ExchangeRate = 1.00m;

        var result = await gateway.StampAsync(request);

        Assert.Equal(PaymentComplementStampingGatewayOutcome.Stamped, result.Outcome);

        var decodedJson = DecodePayloadJson(handler);
        using var json = JsonDocument.Parse(decodedJson);
        var tipoCambioP = json.RootElement.GetProperty("Comprobante").GetProperty("Complemento")[0].GetProperty("Pagos20").GetProperty("Pago")[0].GetProperty("TipoCambioP").GetString();
        Assert.Equal("1", tipoCambioP);
        Assert.DoesNotContain("\"TipoCambioP\":\"1.0\"", decodedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"TipoCambioP\":\"1.00\"", decodedJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StampAsync_Normalizes_EquivalenciaDR_To_1_For_SameCurrency_When_Equivalence_Is_Null()
    {
        var handler = CreateSuccessfulHandler();
        var gateway = CreateGateway(handler);
        var request = CreateRequest();
        request.RelatedDocuments[0].CurrencyEquivalence = null;

        var result = await gateway.StampAsync(request);

        Assert.Equal(PaymentComplementStampingGatewayOutcome.Stamped, result.Outcome);

        var decodedJson = DecodePayloadJson(handler);
        using var json = JsonDocument.Parse(decodedJson);
        var equivalenciaDr = json.RootElement
            .GetProperty("Comprobante")
            .GetProperty("Complemento")[0]
            .GetProperty("Pagos20")
            .GetProperty("Pago")[0]
            .GetProperty("DoctoRelacionado")[0]
            .GetProperty("EquivalenciaDR")
            .GetString();
        Assert.Equal("1", equivalenciaDr);
    }

    [Fact]
    public async Task StampAsync_Normalizes_EquivalenciaDR_To_1_For_SameCurrency_When_Equivalence_Is_Zero()
    {
        var handler = CreateSuccessfulHandler();
        var gateway = CreateGateway(handler);
        var request = CreateRequest();
        request.RelatedDocuments[0].CurrencyEquivalence = 0m;

        var result = await gateway.StampAsync(request);

        Assert.Equal(PaymentComplementStampingGatewayOutcome.Stamped, result.Outcome);

        var decodedJson = DecodePayloadJson(handler);
        using var json = JsonDocument.Parse(decodedJson);
        var equivalenciaDr = json.RootElement
            .GetProperty("Comprobante")
            .GetProperty("Complemento")[0]
            .GetProperty("Pagos20")
            .GetProperty("Pago")[0]
            .GetProperty("DoctoRelacionado")[0]
            .GetProperty("EquivalenciaDR")
            .GetString();
        Assert.Equal("1", equivalenciaDr);
        Assert.DoesNotContain("\"EquivalenciaDR\":0", decodedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"EquivalenciaDR\":\"0\"", decodedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"EquivalenciaDR\":\"0.000000\"", decodedJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StampAsync_Normalizes_EquivalenciaDR_To_1_For_SameCurrency_When_Equivalence_Is_OneDecimal()
    {
        var handler = CreateSuccessfulHandler();
        var gateway = CreateGateway(handler);
        var request = CreateRequest();
        request.RelatedDocuments[0].CurrencyEquivalence = 1.00m;

        var result = await gateway.StampAsync(request);

        Assert.Equal(PaymentComplementStampingGatewayOutcome.Stamped, result.Outcome);

        var decodedJson = DecodePayloadJson(handler);
        using var json = JsonDocument.Parse(decodedJson);
        var equivalenciaDr = json.RootElement
            .GetProperty("Comprobante")
            .GetProperty("Complemento")[0]
            .GetProperty("Pagos20")
            .GetProperty("Pago")[0]
            .GetProperty("DoctoRelacionado")[0]
            .GetProperty("EquivalenciaDR")
            .GetString();
        Assert.Equal("1", equivalenciaDr);
        Assert.DoesNotContain("\"EquivalenciaDR\":\"1.0\"", decodedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"EquivalenciaDR\":\"1.00\"", decodedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"EquivalenciaDR\":\"1.000000\"", decodedJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StampAsync_Normalizes_EquivalenciaDR_To_1_For_SameForeignCurrency()
    {
        var handler = CreateSuccessfulHandler();
        var gateway = CreateGateway(handler);
        var request = CreateRequest();
        request.Payments[0].CurrencyCode = "USD";
        request.Payments[0].ExchangeRate = 17.123456m;
        request.RelatedDocuments[0].CurrencyCode = "USD";
        request.RelatedDocuments[0].CurrencyEquivalence = null;

        var result = await gateway.StampAsync(request);

        Assert.Equal(PaymentComplementStampingGatewayOutcome.Stamped, result.Outcome);

        var decodedJson = DecodePayloadJson(handler);
        using var json = JsonDocument.Parse(decodedJson);
        var pago = json.RootElement.GetProperty("Comprobante").GetProperty("Complemento")[0].GetProperty("Pagos20").GetProperty("Pago")[0];
        Assert.Equal("USD", pago.GetProperty("MonedaP").GetString());
        Assert.Equal("17.123456", pago.GetProperty("TipoCambioP").GetString());
        Assert.Equal("USD", pago.GetProperty("DoctoRelacionado")[0].GetProperty("MonedaDR").GetString());
        Assert.Equal("1", pago.GetProperty("DoctoRelacionado")[0].GetProperty("EquivalenciaDR").GetString());
    }

    [Fact]
    public async Task StampAsync_Serializes_ForeignCurrency_TipoCambioP_With_InvariantCulture()
    {
        var handler = CreateSuccessfulHandler();
        var gateway = CreateGateway(handler);
        var request = CreateRequest();
        request.Payments[0].CurrencyCode = "USD";
        request.Payments[0].ExchangeRate = 17.123456m;
        request.RelatedDocuments[0].CurrencyCode = "USD";
        request.RelatedDocuments[0].CurrencyEquivalence = null;

        var result = await gateway.StampAsync(request);

        Assert.Equal(PaymentComplementStampingGatewayOutcome.Stamped, result.Outcome);

        var decodedJson = DecodePayloadJson(handler);
        using var json = JsonDocument.Parse(decodedJson);
        var pago = json.RootElement.GetProperty("Comprobante").GetProperty("Complemento")[0].GetProperty("Pagos20").GetProperty("Pago")[0];
        Assert.Equal("USD", pago.GetProperty("MonedaP").GetString());
        Assert.Equal("17.123456", pago.GetProperty("TipoCambioP").GetString());
        Assert.DoesNotContain("\"TipoCambioP\":\"1\"", decodedJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StampAsync_Normalizes_EquivalenciaDR_To_1_For_MultipleRelatedDocuments_In_The_SamePayment()
    {
        var handler = CreateSuccessfulHandler();
        var gateway = CreateGateway(handler);
        var request = CreateRequest();
        request.RelatedDocuments.Add(new PaymentComplementStampingRequestRelatedDocument
        {
            AccountsReceivablePaymentId = 10,
            AccountsReceivableInvoiceId = 2,
            FiscalDocumentId = 3,
            RelatedDocumentUuid = "UUID-REL-2",
            Series = "A",
            Folio = "101",
            InstallmentNumber = 2,
            PreviousBalance = 50m,
            PaidAmount = 50m,
            RemainingBalance = 0m,
            CurrencyCode = "MXN",
            CurrencyEquivalence = 0m,
            TaxObjectCode = "01"
        });
        request.Payments[0].RelatedDocuments = request.RelatedDocuments;

        var result = await gateway.StampAsync(request);

        Assert.Equal(PaymentComplementStampingGatewayOutcome.Stamped, result.Outcome);

        var decodedJson = DecodePayloadJson(handler);
        using var json = JsonDocument.Parse(decodedJson);
        var doctos = json.RootElement.GetProperty("Comprobante").GetProperty("Complemento")[0].GetProperty("Pagos20").GetProperty("Pago")[0].GetProperty("DoctoRelacionado");
        Assert.Equal(2, doctos.GetArrayLength());
        Assert.All(doctos.EnumerateArray().Select(docto => docto.GetProperty("EquivalenciaDR").GetString()), equivalenciaDr => Assert.Equal("1", equivalenciaDr));
    }

    [Fact]
    public async Task StampAsync_Uses_The_Parent_Pago_Currency_For_Each_DoctoRelacionado_EquivalenciaDR()
    {
        var handler = CreateSuccessfulHandler();
        var gateway = CreateGateway(handler);
        var request = CreateRequest();
        request.TotalPaymentsAmount = 300m;
        request.RelatedDocuments =
        [
            new PaymentComplementStampingRequestRelatedDocument
            {
                AccountsReceivablePaymentId = 10,
                AccountsReceivableInvoiceId = 1,
                FiscalDocumentId = 2,
                RelatedDocumentUuid = "UUID-REL-USD",
                Series = "USD",
                Folio = "100",
                InstallmentNumber = 1,
                PreviousBalance = 100m,
                PaidAmount = 100m,
                RemainingBalance = 0m,
                CurrencyCode = "USD",
                CurrencyEquivalence = null,
                TaxObjectCode = "01"
            },
            new PaymentComplementStampingRequestRelatedDocument
            {
                AccountsReceivablePaymentId = 20,
                AccountsReceivableInvoiceId = 2,
                FiscalDocumentId = 3,
                RelatedDocumentUuid = "UUID-REL-MXN-USD",
                Series = "MXN",
                Folio = "200",
                InstallmentNumber = 1,
                PreviousBalance = 200m,
                PaidAmount = 200m,
                RemainingBalance = 0m,
                CurrencyCode = "USD",
                CurrencyEquivalence = 0.051234m,
                TaxObjectCode = "01"
            }
        ];
        request.Payments =
        [
            new PaymentComplementStampingRequestPayment
            {
                AccountsReceivablePaymentId = 10,
                PaymentDateUtc = new DateTime(2026, 3, 24, 15, 0, 0, DateTimeKind.Utc),
                PaymentFormSat = "03",
                CurrencyCode = "USD",
                Amount = 100m,
                ExchangeRate = 17.123456m,
                RelatedDocuments =
                [
                    request.RelatedDocuments[0]
                ]
            },
            new PaymentComplementStampingRequestPayment
            {
                AccountsReceivablePaymentId = 20,
                PaymentDateUtc = new DateTime(2026, 3, 24, 16, 0, 0, DateTimeKind.Utc),
                PaymentFormSat = "03",
                CurrencyCode = "MXN",
                Amount = 200m,
                ExchangeRate = null,
                RelatedDocuments =
                [
                    request.RelatedDocuments[1]
                ]
            }
        ];

        var result = await gateway.StampAsync(request);

        Assert.Equal(PaymentComplementStampingGatewayOutcome.Stamped, result.Outcome);

        var decodedJson = DecodePayloadJson(handler);
        using var json = JsonDocument.Parse(decodedJson);
        var pagos = json.RootElement.GetProperty("Comprobante").GetProperty("Complemento")[0].GetProperty("Pagos20").GetProperty("Pago");
        Assert.Equal("1", pagos[0].GetProperty("DoctoRelacionado")[0].GetProperty("EquivalenciaDR").GetString());
        Assert.Equal("0.051234", pagos[1].GetProperty("DoctoRelacionado")[0].GetProperty("EquivalenciaDR").GetString());
        Assert.DoesNotContain("\"EquivalenciaDR\":\"1.000000\"", decodedJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StampAsync_Serializes_ForeignCurrency_EquivalenciaDR_With_InvariantCulture()
    {
        var handler = CreateSuccessfulHandler();
        var gateway = CreateGateway(handler);
        var request = CreateRequest();
        request.RelatedDocuments[0].CurrencyCode = "USD";
        request.RelatedDocuments[0].CurrencyEquivalence = 17.123456m;

        var result = await gateway.StampAsync(request);

        Assert.Equal(PaymentComplementStampingGatewayOutcome.Stamped, result.Outcome);

        var decodedJson = DecodePayloadJson(handler);
        using var json = JsonDocument.Parse(decodedJson);
        var docto = json.RootElement.GetProperty("Comprobante").GetProperty("Complemento")[0].GetProperty("Pagos20").GetProperty("Pago")[0].GetProperty("DoctoRelacionado")[0];
        Assert.Equal("USD", docto.GetProperty("MonedaDR").GetString());
        Assert.Equal("17.123456", docto.GetProperty("EquivalenciaDR").GetString());
        Assert.DoesNotContain("\"EquivalenciaDR\":\"1\"", decodedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"EquivalenciaDR\":\"17,123456\"", decodedJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0d)]
    public async Task StampAsync_Fails_Before_ProviderCall_When_ForeignCurrency_ExchangeRate_Is_Missing_Or_Zero(double? exchangeRate)
    {
        var handler = CreateSuccessfulHandler();
        var gateway = CreateGateway(handler);
        var request = CreateRequest();
        request.Payments[0].CurrencyCode = "USD";
        request.Payments[0].ExchangeRate = exchangeRate.HasValue ? (decimal)exchangeRate.Value : null;
        request.RelatedDocuments[0].CurrencyCode = "USD";
        request.RelatedDocuments[0].CurrencyEquivalence = null;

        var result = await gateway.StampAsync(request);

        Assert.Equal(PaymentComplementStampingGatewayOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("TipoCambioP requerido para MonedaP 'USD'", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task StampAsync_Fails_Before_ProviderCall_When_ForeignCurrency_EquivalenciaDR_Is_Missing()
    {
        var handler = CreateSuccessfulHandler();
        var gateway = CreateGateway(handler);
        var request = CreateRequest();
        request.RelatedDocuments[0].CurrencyCode = "USD";
        request.RelatedDocuments[0].CurrencyEquivalence = null;

        var result = await gateway.StampAsync(request);

        Assert.Equal(PaymentComplementStampingGatewayOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("EquivalenciaDR requerida y mayor a 0 cuando MonedaDR 'USD' es distinta de MonedaP 'MXN'", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task StampAsync_Fails_Before_ProviderCall_When_ForeignCurrency_EquivalenciaDR_Is_Zero()
    {
        var handler = CreateSuccessfulHandler();
        var gateway = CreateGateway(handler);
        var request = CreateRequest();
        request.RelatedDocuments[0].CurrencyCode = "USD";
        request.RelatedDocuments[0].CurrencyEquivalence = 0m;

        var result = await gateway.StampAsync(request);

        Assert.Equal(PaymentComplementStampingGatewayOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("EquivalenciaDR requerida y mayor a 0 cuando MonedaDR 'USD' es distinta de MonedaP 'MXN'", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public void ValidatePaymentComplementPayloadJson_Rejects_LegacyObjectComplemento()
    {
        const string legacyPayload = """
            {
              "Comprobante": {
                "Version": "4.0",
                "Fecha": "2026-03-24T09:00:00",
                "Moneda": "XXX",
                "TipoDeComprobante": "P",
                "Exportacion": "01",
                "LugarExpedicion": "01000",
                "SubTotal": 0,
                "Total": 0,
                "Emisor": {
                  "Rfc": "AAA010101AAA",
                  "Nombre": "Issuer SA",
                  "RegimenFiscal": "601"
                },
                "Receptor": {
                  "Rfc": "BBB010101BBB",
                  "Nombre": "Receiver SA",
                  "DomicilioFiscalReceptor": "02000",
                  "RegimenFiscalReceptor": "601",
                  "UsoCFDI": "CP01"
                },
                "Conceptos": [
                  {
                    "ClaveProdServ": "84111506",
                    "Cantidad": 1,
                    "ClaveUnidad": "ACT",
                    "Descripcion": "Pago",
                    "ValorUnitario": 0,
                    "Importe": 0,
                    "ObjetoImp": "01"
                  }
                ],
                "Complemento": {
                  "Pagos20": {
                    "Version": "2.0",
                    "Totales": {
                      "MontoTotalPagos": 123.45
                    },
                    "Pago": [
                      {
                        "FechaPago": "2026-03-24T09:00:00",
                        "FormaDePagoP": "03",
                        "MonedaP": "MXN",
                        "Monto": 123.45,
                        "DoctoRelacionado": [
                          {
                            "IdDocumento": "UUID-REL-1",
                            "MonedaDR": "MXN",
                            "NumParcialidad": 1,
                            "ImpSaldoAnt": 123.45,
                            "ImpPagado": 123.45,
                            "ImpSaldoInsoluto": 0,
                            "ObjetoImpDR": "02"
                          }
                        ]
                      }
                    ]
                  }
                }
              }
            }
            """;

        var validationError = InvokePayloadValidation(legacyPayload);

        Assert.NotNull(validationError);
        Assert.Contains("Comprobante.Complemento[0].Pagos20", validationError, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatePaymentComplementPayloadJson_Rejects_MxnPago_With_TipoCambioP_Distinct_From_1()
    {
        const string invalidTipoCambioPayload = """
            {
              "Comprobante": {
                "Version": "4.0",
                "Fecha": "2026-03-24T09:00:00",
                "Moneda": "XXX",
                "TipoDeComprobante": "P",
                "Exportacion": "01",
                "LugarExpedicion": "01000",
                "SubTotal": 0,
                "Total": 0,
                "Emisor": {
                  "Rfc": "AAA010101AAA",
                  "Nombre": "Issuer SA",
                  "RegimenFiscal": "601"
                },
                "Receptor": {
                  "Rfc": "BBB010101BBB",
                  "Nombre": "Receiver SA",
                  "DomicilioFiscalReceptor": "02000",
                  "RegimenFiscalReceptor": "601",
                  "UsoCFDI": "CP01"
                },
                "Conceptos": [
                  {
                    "ClaveProdServ": "84111506",
                    "Cantidad": 1,
                    "ClaveUnidad": "ACT",
                    "Descripcion": "Pago",
                    "ValorUnitario": 0,
                    "Importe": 0,
                    "ObjetoImp": "01"
                  }
                ],
                "Complemento": [
                  {
                    "Pagos20": {
                      "Version": "2.0",
                      "Totales": {
                        "MontoTotalPagos": 123.45
                      },
                      "Pago": [
                        {
                          "FechaPago": "2026-03-24T09:00:00",
                          "FormaDePagoP": "03",
                          "MonedaP": "MXN",
                          "TipoCambioP": "0",
                          "Monto": 123.45,
                          "DoctoRelacionado": [
                            {
                              "IdDocumento": "UUID-REL-1",
                              "MonedaDR": "MXN",
                              "NumParcialidad": 1,
                              "ImpSaldoAnt": 123.45,
                              "ImpPagado": 123.45,
                              "ImpSaldoInsoluto": 0,
                              "ObjetoImpDR": "02"
                            }
                          ]
                        }
                      ]
                    }
                  }
                ]
              }
            }
            """;

        var validationError = InvokePayloadValidation(invalidTipoCambioPayload);

        Assert.NotNull(validationError);
        Assert.Equal(
            "Payload REP invalido para FacturaloPlus: cuando MonedaP es MXN, TipoCambioP debe ser exactamente '1'.",
            validationError);
    }

    [Fact]
    public void ValidatePaymentComplementPayloadJson_Rejects_SameCurrency_EquivalenciaDR_Distinct_From_1()
    {
        const string invalidEquivalenciaPayload = """
            {
              "Comprobante": {
                "Version": "4.0",
                "Fecha": "2026-03-24T09:00:00",
                "Moneda": "XXX",
                "TipoDeComprobante": "P",
                "Exportacion": "01",
                "LugarExpedicion": "01000",
                "SubTotal": 0,
                "Total": 0,
                "Emisor": {
                  "Rfc": "AAA010101AAA",
                  "Nombre": "Issuer SA",
                  "RegimenFiscal": "601"
                },
                "Receptor": {
                  "Rfc": "BBB010101BBB",
                  "Nombre": "Receiver SA",
                  "DomicilioFiscalReceptor": "02000",
                  "RegimenFiscalReceptor": "601",
                  "UsoCFDI": "CP01"
                },
                "Conceptos": [
                  {
                    "ClaveProdServ": "84111506",
                    "Cantidad": 1,
                    "ClaveUnidad": "ACT",
                    "Descripcion": "Pago",
                    "ValorUnitario": 0,
                    "Importe": 0,
                    "ObjetoImp": "01"
                  }
                ],
                "Complemento": [
                  {
                    "Pagos20": {
                      "Version": "2.0",
                      "Totales": {
                        "MontoTotalPagos": 123.45
                      },
                      "Pago": [
                        {
                          "FechaPago": "2026-03-24T09:00:00",
                          "FormaDePagoP": "03",
                          "MonedaP": "MXN",
                          "TipoCambioP": "1",
                          "Monto": 123.45,
                          "DoctoRelacionado": [
                            {
                              "IdDocumento": "UUID-REL-1",
                              "MonedaDR": "MXN",
                              "EquivalenciaDR": "0",
                              "NumParcialidad": 1,
                              "ImpSaldoAnt": 123.45,
                              "ImpPagado": 123.45,
                              "ImpSaldoInsoluto": 0,
                              "ObjetoImpDR": "02"
                            }
                          ]
                        }
                      ]
                    }
                  }
                ]
              }
            }
            """;

        var validationError = InvokePayloadValidation(invalidEquivalenciaPayload);

        Assert.NotNull(validationError);
        Assert.Equal(
            "Payload REP invalido para FacturaloPlus: cuando MonedaDR es igual a MonedaP, EquivalenciaDR debe ser exactamente '1'.",
            validationError);
    }

    [Fact]
    public void ValidatePaymentComplementPayloadJson_Accepts_Sanitized_FacturaloPlus_Fixture()
    {
        var fixturePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "Fixtures",
            "FacturaloPlus",
            "payment-complement-pagos20.sanitized.json"));

        var payloadJson = File.ReadAllText(fixturePath);

        var validationError = InvokePayloadValidation(payloadJson);

        Assert.Null(validationError);

        using var json = JsonDocument.Parse(payloadJson);
        var pagos20 = json.RootElement.GetProperty("Comprobante").GetProperty("Complemento")[0].GetProperty("Pagos20");
        Assert.Equal("2.0", pagos20.GetProperty("Version").GetString());
        Assert.Equal("1", pagos20.GetProperty("Pago")[0].GetProperty("TipoCambioP").GetString());
        Assert.Equal("MXN", pagos20.GetProperty("Pago")[0].GetProperty("DoctoRelacionado")[0].GetProperty("MonedaDR").GetString());
        Assert.Equal("1", pagos20.GetProperty("Pago")[0].GetProperty("DoctoRelacionado")[0].GetProperty("EquivalenciaDR").GetString());
    }

    [Fact]
    public async Task StampAsync_Treats_Code200_With_NestedDataString_And_StampedXml_As_Success()
    {
        const string stampedXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <cfdi:Comprobante xmlns:cfdi="http://www.sat.gob.mx/cfd/4" xmlns:tfd="http://www.sat.gob.mx/TimbreFiscalDigital" Version="4.0">
              <cfdi:Complemento>
                <tfd:TimbreFiscalDigital UUID="PC-UUID-1111-2222-3333-444455556666" FechaTimbrado="2026-03-24T10:26:55" SelloSAT="SELLO-SAT" NoCertificadoSAT="00001000000500001234" Version="1.1" />
              </cfdi:Complemento>
            </cfdi:Comprobante>
            """;
        var nestedDataJson = JsonSerializer.Serialize(new { XML = stampedXml });
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\n" +
                "  \"code\": \"200\",\n" +
                "  \"message\": \"Solicitud procesada con éxito. - Complemento timbrado.\",\n" +
                "  \"data\": " + JsonSerializer.Serialize(nestedDataJson) + "\n" +
                "}",
                Encoding.UTF8,
                "application/json")
        });

        var gateway = CreateGateway(handler);

        var result = await gateway.StampAsync(CreateRequest());

        Assert.Equal(PaymentComplementStampingGatewayOutcome.Stamped, result.Outcome);
        Assert.Equal("200", result.ProviderCode);
        Assert.Equal("PC-UUID-1111-2222-3333-444455556666", result.Uuid);
        Assert.Equal(stampedXml, result.XmlContent);
        Assert.NotNull(result.StampedAtUtc);
    }

    [Fact]
    public async Task StampAsync_Treats_Code200_With_NestedDataObject_And_FlatUuid_As_Success()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "code": "200",
                  "message": "Solicitud procesada con éxito. - Complemento timbrado.",
                  "data": {
                    "uuid": "UUID-PC-OK-1",
                    "xml": "<cfdi:Comprobante xmlns:cfdi=\"http://www.sat.gob.mx/cfd/4\" />"
                  }
                }
                """, Encoding.UTF8, "application/json")
        });

        var gateway = CreateGateway(handler);

        var result = await gateway.StampAsync(CreateRequest());

        Assert.Equal(PaymentComplementStampingGatewayOutcome.Stamped, result.Outcome);
        Assert.Equal("UUID-PC-OK-1", result.Uuid);
        Assert.Contains("Complemento timbrado", result.ProviderMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StampAsync_Infers_Success_From_Xml_When_FlatIdentifier_Is_Missing()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "code": "200",
                  "message": "Solicitud procesada con éxito. - Complemento timbrado.",
                  "data": {
                    "xml": "<?xml version=\"1.0\" encoding=\"utf-8\"?><cfdi:Comprobante xmlns:cfdi=\"http://www.sat.gob.mx/cfd/4\" xmlns:tfd=\"http://www.sat.gob.mx/TimbreFiscalDigital\" Version=\"4.0\"><cfdi:Complemento><tfd:TimbreFiscalDigital UUID=\"PC-UUID-XML-ONLY\" FechaTimbrado=\"2026-03-24T10:26:55\" Version=\"1.1\" /></cfdi:Complemento></cfdi:Comprobante>"
                  }
                }
                """, Encoding.UTF8, "application/json")
        });

        var gateway = CreateGateway(handler);

        var result = await gateway.StampAsync(CreateRequest());

        Assert.Equal(PaymentComplementStampingGatewayOutcome.Stamped, result.Outcome);
        Assert.Equal("PC-UUID-XML-ONLY", result.Uuid);
        Assert.NotNull(result.StampedAtUtc);
        Assert.Contains("Complemento timbrado", result.ProviderMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StampAsync_Builds_MultiplePagos_WithTransfers_AndRetentions()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "code": "200",
                  "message": "Solicitud procesada con éxito. - Complemento timbrado.",
                  "data": {
                    "uuid": "UUID-PC-MULTI-1",
                    "xml": "<cfdi:Comprobante xmlns:cfdi=\"http://www.sat.gob.mx/cfd/4\" />"
                  }
                }
                """, Encoding.UTF8, "application/json")
        });

        var gateway = CreateGateway(handler);
        var request = CreateRequest();
        request.TotalPaymentsAmount = 173.45m;
        request.Payments =
        [
            new PaymentComplementStampingRequestPayment
            {
                AccountsReceivablePaymentId = 10,
                PaymentDateUtc = new DateTime(2026, 3, 24, 15, 0, 0, DateTimeKind.Utc),
                PaymentFormSat = "03",
                CurrencyCode = "MXN",
                Amount = 123.45m,
                RelatedDocuments = request.RelatedDocuments
            },
            new PaymentComplementStampingRequestPayment
            {
                AccountsReceivablePaymentId = 11,
                PaymentDateUtc = new DateTime(2026, 3, 25, 15, 0, 0, DateTimeKind.Utc),
                PaymentFormSat = "03",
                CurrencyCode = "MXN",
                Amount = 50m,
                RelatedDocuments =
                [
                    new PaymentComplementStampingRequestRelatedDocument
                    {
                        AccountsReceivablePaymentId = 11,
                        AccountsReceivableInvoiceId = 2,
                        FiscalDocumentId = 3,
                        RelatedDocumentUuid = "UUID-REL-2",
                        Series = "B",
                        Folio = "200",
                        InstallmentNumber = 2,
                        PreviousBalance = 50m,
                        PaidAmount = 50m,
                        RemainingBalance = 0m,
                        CurrencyCode = "MXN",
                        TaxObjectCode = "02",
                        TaxRetentions =
                        [
                            new PaymentComplementStampingRequestTaxRetention
                            {
                                TaxCode = "001",
                                BaseAmount = 50m,
                                TaxAmount = 5m
                            }
                        ],
                        TaxTransfers =
                        [
                            new PaymentComplementStampingRequestTaxTransfer
                            {
                                TaxCode = "002",
                                FactorType = "Exento",
                                Rate = 0m,
                                BaseAmount = 50m,
                                TaxAmount = 0m
                            }
                        ]
                    }
                ]
            }
        ];

        var result = await gateway.StampAsync(request);

        Assert.Equal(PaymentComplementStampingGatewayOutcome.Stamped, result.Outcome);

        var form = ParseFormBody(handler.LastBody!);
        var decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(form["jsonB64"]));
        using var json = JsonDocument.Parse(decodedJson);
        var pagos20 = json.RootElement.GetProperty("Comprobante").GetProperty("Complemento")[0].GetProperty("Pagos20");
        Assert.Equal(2, pagos20.GetProperty("Pago").GetArrayLength());
        Assert.All(
            pagos20.GetProperty("Pago").EnumerateArray().Select(pago => pago.GetProperty("TipoCambioP").GetString()),
            tipoCambioP => Assert.Equal("1", tipoCambioP));
        Assert.Equal(173.45m, pagos20.GetProperty("Totales").GetProperty("MontoTotalPagos").GetDecimal());
        Assert.Equal(17.03m, pagos20.GetProperty("Totales").GetProperty("TotalTrasladosImpuestoIVA16").GetDecimal());
        Assert.Equal(5m, pagos20.GetProperty("Totales").GetProperty("TotalRetencionesISR").GetDecimal());
        Assert.Equal(50m, pagos20.GetProperty("Totales").GetProperty("TotalTrasladosBaseIVAExento").GetDecimal());
    }

    [Fact]
    public async Task StampAsync_RealProviderRejection_Remains_Rejected()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""
                {
                  "code": "CFDI40101",
                  "message": "Complemento inválido",
                  "errorCode": "CFDI40101",
                  "errorMessage": "Complemento inválido"
                }
                """, Encoding.UTF8, "application/json")
        });

        var gateway = CreateGateway(handler);

        var result = await gateway.StampAsync(CreateRequest());

        Assert.Equal(PaymentComplementStampingGatewayOutcome.Rejected, result.Outcome);
        Assert.Equal("CFDI40101", result.ErrorCode);
    }

    private static FacturaloPlusPaymentComplementStampingGateway CreateGateway(HttpMessageHandler handler)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=FacturaloPlus Test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var serialBytes = Encoding.ASCII.GetBytes("30001000000500003416");
        var certificate = request.Create(
            new X500DistinguishedName("CN=FacturaloPlus Test"),
            X509SignatureGenerator.CreateForRSA(rsa, RSASignaturePadding.Pkcs1),
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1),
            serialBytes);
        var certificatePem = new string(PemEncoding.Write("CERTIFICATE", certificate.Export(X509ContentType.Cert)));

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://dev.facturaloplus.com/api/rest/servicio/")
        };
        var secretResolver = new RecordingSecretResolver(new Dictionary<string, string?>
        {
            ["FACTURALOPLUS_API_KEY_REFERENCE"] = "APIKEY-TEST",
            ["CERT_REF"] = certificatePem,
            ["KEY_REF"] = "PRIVATE-KEY-PEM",
            ["PWD_REF"] = "PRIVATE-KEY-PASSWORD"
        });

        return new FacturaloPlusPaymentComplementStampingGateway(
            client,
            Options.Create(new FacturaloPlusOptions
            {
                BaseUrl = "https://dev.facturaloplus.com/api/rest/servicio/",
                PaymentComplementStampPath = "timbrarJSON3",
                ApiKeyReference = "FACTURALOPLUS_API_KEY_REFERENCE",
                ApiKeyHeaderName = "X-Api-Key"
            }),
            secretResolver);
    }

    private static RecordingHandler CreateSuccessfulHandler()
    {
        return new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "code": "200",
                  "message": "Solicitud procesada con éxito. - Complemento timbrado.",
                  "data": {
                    "uuid": "UUID-PC-OK-1",
                    "xml": "<cfdi:Comprobante xmlns:cfdi=\"http://www.sat.gob.mx/cfd/4\" />"
                  }
                }
                """, Encoding.UTF8, "application/json")
        });
    }

    private static PaymentComplementStampingRequest CreateRequest()
    {
        var request = new PaymentComplementStampingRequest
        {
            PaymentComplementDocumentId = 50,
            PacEnvironment = "Sandbox",
            CertificateReference = "CERT_REF",
            PrivateKeyReference = "KEY_REF",
            PrivateKeyPasswordReference = "PWD_REF",
            CfdiVersion = "4.0",
            DocumentType = "P",
            IssuedAtUtc = new DateTime(2026, 3, 24, 15, 0, 0, DateTimeKind.Utc),
            PaymentDateUtc = new DateTime(2026, 3, 24, 15, 0, 0, DateTimeKind.Utc),
            PaymentFormSat = "03",
            CurrencyCode = "MXN",
            TotalPaymentsAmount = 123.45m,
            IssuerRfc = "AAA010101AAA",
            IssuerLegalName = "Issuer SA",
            IssuerFiscalRegimeCode = "601",
            IssuerPostalCode = "01000",
            ReceiverRfc = "BBB010101BBB",
            ReceiverLegalName = "Receiver SA",
            ReceiverFiscalRegimeCode = "601",
            ReceiverPostalCode = "02000",
            RelatedDocuments =
            [
                new PaymentComplementStampingRequestRelatedDocument
                {
                    AccountsReceivablePaymentId = 10,
                    AccountsReceivableInvoiceId = 1,
                    FiscalDocumentId = 2,
                    RelatedDocumentUuid = "UUID-REL-1",
                    Series = "A",
                    Folio = "100",
                    InstallmentNumber = 1,
                    PreviousBalance = 123.45m,
                    PaidAmount = 123.45m,
                    RemainingBalance = 0m,
                    CurrencyCode = "MXN",
                    TaxObjectCode = "02",
                    TaxTransfers =
                    [
                        new PaymentComplementStampingRequestTaxTransfer
                        {
                            TaxCode = "002",
                            FactorType = "Tasa",
                            Rate = 0.16m,
                            BaseAmount = 106.42m,
                            TaxAmount = 17.03m
                        }
                    ]
                }
            ],
            Payments =
            [
                new PaymentComplementStampingRequestPayment
                {
                    AccountsReceivablePaymentId = 10,
                    PaymentDateUtc = new DateTime(2026, 3, 24, 15, 0, 0, DateTimeKind.Utc),
                    PaymentFormSat = "03",
                    CurrencyCode = "MXN",
                    Amount = 123.45m,
                    RelatedDocuments = []
                }
            ]
        };

        request.Payments[0].RelatedDocuments = request.RelatedDocuments;
        return request;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastContentType { get; private set; }

        public string? LastBody { get; private set; }

        public RecordingHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastContentType = request.Content?.Headers.ContentType?.MediaType;
            LastBody = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            return Task.FromResult(_response);
        }
    }

    private static Dictionary<string, string> ParseFormBody(string body)
    {
        return body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                pieces => Uri.UnescapeDataString(pieces[0]),
                pieces => Uri.UnescapeDataString(pieces.Length > 1 ? pieces[1] : string.Empty));
    }

    private static string DecodePayloadJson(RecordingHandler handler)
    {
        Assert.NotNull(handler.LastBody);
        var form = ParseFormBody(handler.LastBody!);
        Assert.True(form.ContainsKey("jsonB64"));
        return Encoding.UTF8.GetString(Convert.FromBase64String(form["jsonB64"]));
    }

    private static string? InvokePayloadValidation(string payloadJson)
    {
        var method = typeof(FacturaloPlusPaymentComplementStampingGateway).GetMethod(
            "ValidatePaymentComplementPayloadJson",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        return (string?)method!.Invoke(null, [payloadJson]);
    }

    private sealed class RecordingSecretResolver : ISecretReferenceResolver
    {
        private readonly IReadOnlyDictionary<string, string?> _values;

        public RecordingSecretResolver(IReadOnlyDictionary<string, string?> values)
        {
            _values = values;
        }

        public Task<string?> ResolveAsync(string referenceKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_values.TryGetValue(referenceKey, out var value) ? value : null);
        }
    }
}
