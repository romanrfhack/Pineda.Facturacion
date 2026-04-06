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
        Assert.Equal("CP01", comprobante.GetProperty("Receptor").GetProperty("UsoCFDI").GetString());
        Assert.Equal("03", comprobante.GetProperty("Complemento").GetProperty("Pagos20").GetProperty("Pago")[0].GetProperty("FormaDePagoP").GetString());
        Assert.Equal("UUID-REL-1", comprobante.GetProperty("Complemento").GetProperty("Pagos20").GetProperty("Pago")[0].GetProperty("DoctoRelacionado")[0].GetProperty("IdDocumento").GetString());
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

    private static PaymentComplementStampingRequest CreateRequest()
    {
        return new PaymentComplementStampingRequest
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
                    AccountsReceivableInvoiceId = 1,
                    FiscalDocumentId = 2,
                    RelatedDocumentUuid = "UUID-REL-1",
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
            ]
        };
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
