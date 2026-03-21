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

public class FacturaloPlusStampingGatewayTests
{
    [Fact]
    public async Task StampAsync_Builds_FormUrlEncoded_Request_For_TimbrarJson3()
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
        var certificateBase64 = Convert.ToBase64String(certificate.Export(X509ContentType.Cert));

        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "success": true,
                  "trackingId": "TRACK-1",
                  "uuid": "UUID-1",
                  "stampedAtUtc": "2026-03-21T12:00:00Z",
                  "xmlContent": "<cfdi:Comprobante Version=\"4.0\" />"
                }
                """, Encoding.UTF8, "application/json")
        });

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

        var gateway = new FacturaloPlusStampingGateway(
            client,
            Options.Create(new FacturaloPlusOptions
            {
                BaseUrl = "https://dev.facturaloplus.com/api/rest/servicio/",
                StampPath = "timbrarJSON3",
                ApiKeyReference = "FACTURALOPLUS_API_KEY_REFERENCE",
                ApiKeyHeaderName = "X-Api-Key"
            }),
            secretResolver);

        var result = await gateway.StampAsync(CreateRequest());

        Assert.Equal(FiscalStampingGatewayOutcome.Stamped, result.Outcome);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.EndsWith("/timbrarJSON3", handler.LastRequest.RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.False(handler.LastRequest.Headers.Contains("X-Api-Key"));
        Assert.Equal("application/x-www-form-urlencoded", handler.LastContentType);

        var form = ParseFormBody(handler.LastBody!);

        Assert.Equal("APIKEY-TEST", form["apikey"]);
        Assert.Equal("PRIVATE-KEY-PEM", form["keyPEM"]);
        Assert.Equal(certificatePem, form["cerPEM"]);
        Assert.True(form.ContainsKey("jsonB64"));

        var decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(form["jsonB64"]));
        using var json = JsonDocument.Parse(decodedJson);
        var comprobante = json.RootElement.GetProperty("Comprobante");
        Assert.Equal("4.0", comprobante.GetProperty("Version").GetString());
        Assert.Equal("2026-03-21T12:00:00", comprobante.GetProperty("Fecha").GetString());
        Assert.Equal("I", comprobante.GetProperty("TipoDeComprobante").GetString());
        Assert.Equal("PPD", comprobante.GetProperty("MetodoPago").GetString());
        Assert.Equal("99", comprobante.GetProperty("FormaPago").GetString());
        Assert.Equal("01", comprobante.GetProperty("Exportacion").GetString());
        Assert.Equal("01000", comprobante.GetProperty("LugarExpedicion").GetString());
        Assert.Equal(848m, comprobante.GetProperty("SubTotal").GetDecimal());
        Assert.Equal(283m, comprobante.GetProperty("Descuento").GetDecimal());
        Assert.Equal(565m, comprobante.GetProperty("Total").GetDecimal());
        Assert.Equal("30001000000500003416", comprobante.GetProperty("NoCertificado").GetString());
        Assert.Equal(certificateBase64, comprobante.GetProperty("Certificado").GetString());
        Assert.Equal("AAA010101AAA", comprobante.GetProperty("Emisor").GetProperty("Rfc").GetString());
        Assert.Equal("Receiver SA", comprobante.GetProperty("Receptor").GetProperty("Nombre").GetString());
        Assert.Equal("G03", comprobante.GetProperty("Receptor").GetProperty("UsoCFDI").GetString());
        Assert.Equal("01010101", comprobante.GetProperty("Conceptos")[0].GetProperty("ClaveProdServ").GetString());
        Assert.Equal("02", comprobante.GetProperty("Conceptos")[0].GetProperty("ObjetoImp").GetString());
        Assert.Equal(848m, comprobante.GetProperty("Conceptos")[0].GetProperty("Importe").GetDecimal());
        Assert.Equal(283m, comprobante.GetProperty("Conceptos")[0].GetProperty("Descuento").GetDecimal());
        var conceptoTraslado = comprobante
            .GetProperty("Conceptos")[0]
            .GetProperty("Impuestos")
            .GetProperty("Traslados")[0];
        Assert.Equal(565m, conceptoTraslado.GetProperty("Base").GetDecimal());
        Assert.Equal("002", conceptoTraslado.GetProperty("Impuesto").GetString());
        Assert.Equal("Tasa", conceptoTraslado.GetProperty("TipoFactor").GetString());
        Assert.Equal("0.000000", conceptoTraslado.GetProperty("TasaOCuota").GetString());
        Assert.Equal(0m, conceptoTraslado.GetProperty("Importe").GetDecimal());
        var comprobanteTraslado = comprobante.GetProperty("Impuestos").GetProperty("Traslados")[0];
        Assert.Equal(565m, comprobanteTraslado.GetProperty("Base").GetDecimal());
        Assert.Equal("002", comprobanteTraslado.GetProperty("Impuesto").GetString());
        Assert.Equal("Tasa", comprobanteTraslado.GetProperty("TipoFactor").GetString());
        Assert.Equal("0.000000", comprobanteTraslado.GetProperty("TasaOCuota").GetString());
        Assert.Equal(0m, comprobanteTraslado.GetProperty("Importe").GetDecimal());
        Assert.Equal(0m, comprobante.GetProperty("Impuestos").GetProperty("TotalImpuestosTrasladados").GetDecimal());
        Assert.False(json.RootElement.TryGetProperty("issuer", out _));
        Assert.False(json.RootElement.TryGetProperty("Environment", out _));
        Assert.DoesNotContain("PRIVATE-KEY-PEM", decodedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE-KEY-PASSWORD", decodedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"certificate\"", decodedJson, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("FACTURALOPLUS_API_KEY_REFERENCE", secretResolver.RequestedKeys);
        Assert.Contains("CERT_REF", secretResolver.RequestedKeys);
        Assert.Contains("KEY_REF", secretResolver.RequestedKeys);
        Assert.DoesNotContain("PWD_REF", secretResolver.RequestedKeys);
    }

    [Fact]
    public async Task StampAsync_Serializes_TasaOCuota_With_Six_Decimals()
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

        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "success": true,
                  "trackingId": "TRACK-1",
                  "uuid": "UUID-1",
                  "stampedAtUtc": "2026-03-21T12:00:00Z",
                  "xmlContent": "<cfdi:Comprobante Version=\"4.0\" />"
                }
                """, Encoding.UTF8, "application/json")
        });

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://dev.facturaloplus.com/api/rest/servicio/")
        };
        var secretResolver = new RecordingSecretResolver(new Dictionary<string, string?>
        {
            ["FACTURALOPLUS_API_KEY_REFERENCE"] = "APIKEY-TEST",
            ["CERT_REF"] = certificatePem,
            ["KEY_REF"] = "PRIVATE-KEY-PEM"
        });

        var gateway = new FacturaloPlusStampingGateway(
            client,
            Options.Create(new FacturaloPlusOptions
            {
                BaseUrl = "https://dev.facturaloplus.com/api/rest/servicio/",
                StampPath = "timbrarJSON3",
                ApiKeyReference = "FACTURALOPLUS_API_KEY_REFERENCE",
                ApiKeyHeaderName = "X-Api-Key"
            }),
            secretResolver);

        var requestPayload = CreateRequest();
        requestPayload.Subtotal = 100m;
        requestPayload.DiscountTotal = 0m;
        requestPayload.Total = 116m;
        requestPayload.TaxTotal = 16m;
        requestPayload.Items[0].UnitPrice = 100m;
        requestPayload.Items[0].DiscountAmount = 0m;
        requestPayload.Items[0].Subtotal = 100m;
        requestPayload.Items[0].TaxTotal = 16m;
        requestPayload.Items[0].Total = 116m;
        requestPayload.Items[0].VatRate = 0.16m;

        await gateway.StampAsync(requestPayload);

        var form = ParseFormBody(handler.LastBody!);
        var decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(form["jsonB64"]));
        using var json = JsonDocument.Parse(decodedJson);
        var conceptoTraslado = json.RootElement
            .GetProperty("Comprobante")
            .GetProperty("Conceptos")[0]
            .GetProperty("Impuestos")
            .GetProperty("Traslados")[0];

        Assert.Equal("0.160000", conceptoTraslado.GetProperty("TasaOCuota").GetString());
    }

    private static FiscalStampingRequest CreateRequest()
    {
        return new FiscalStampingRequest
        {
            FiscalDocumentId = 40,
            PacEnvironment = "Sandbox",
            CfdiVersion = "4.0",
            DocumentType = "INVOICE",
            IssuedAtUtc = new DateTime(2026, 3, 21, 12, 0, 0, DateTimeKind.Utc),
            CurrencyCode = "MXN",
            ExchangeRate = 1m,
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            PaymentCondition = "CREDITO",
            IsCreditSale = true,
            CreditDays = 7,
            IssuerRfc = "AAA010101AAA",
            IssuerLegalName = "Issuer SA",
            IssuerFiscalRegimeCode = "601",
            IssuerPostalCode = "01000",
            ReceiverRfc = "BBB010101BBB",
            ReceiverLegalName = "Receiver SA",
            ReceiverFiscalRegimeCode = "601",
            ReceiverCfdiUseCode = "G03",
            ReceiverPostalCode = "02000",
            ReceiverCountryCode = "MX",
            Subtotal = 565m,
            DiscountTotal = 283m,
            TaxTotal = 0m,
            Total = 565m,
            CertificateReference = "CERT_REF",
            PrivateKeyReference = "KEY_REF",
            PrivateKeyPasswordReference = "PWD_REF",
            Items =
            [
                new FiscalStampingRequestItem
                {
                    LineNumber = 1,
                    InternalCode = "SKU-1",
                    Description = "Producto",
                    Quantity = 1m,
                    UnitPrice = 848m,
                    DiscountAmount = 283m,
                    Subtotal = 565m,
                    TaxTotal = 0m,
                    Total = 565m,
                    SatProductServiceCode = "01010101",
                    SatUnitCode = "H87",
                    TaxObjectCode = "02",
                    VatRate = 0m,
                    UnitText = "Pieza"
                }
            ]
        };
    }

    private static Dictionary<string, string> ParseFormBody(string body)
    {
        return body
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Split('=', 2))
            .ToDictionary(
                parts => DecodeFormComponent(parts[0]),
                parts => parts.Length > 1 ? DecodeFormComponent(parts[1]) : string.Empty,
                StringComparer.Ordinal);
    }

    private static string DecodeFormComponent(string value)
    {
        return Uri.UnescapeDataString(value.Replace("+", " ", StringComparison.Ordinal));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public RecordingHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }
        public string? LastContentType { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastContentType = request.Content?.Headers.ContentType?.MediaType;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return _response;
        }
    }

    private sealed class RecordingSecretResolver : ISecretReferenceResolver
    {
        private readonly IReadOnlyDictionary<string, string?> _values;

        public RecordingSecretResolver(IReadOnlyDictionary<string, string?> values)
        {
            _values = values;
        }

        public List<string> RequestedKeys { get; } = [];

        public Task<string?> ResolveAsync(string referenceKey, CancellationToken cancellationToken = default)
        {
            RequestedKeys.Add(referenceKey);
            return Task.FromResult(_values.TryGetValue(referenceKey, out var value) ? value : null);
        }
    }
}
