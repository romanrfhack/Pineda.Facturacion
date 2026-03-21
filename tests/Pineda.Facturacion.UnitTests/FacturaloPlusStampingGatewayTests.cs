using System.Net;
using System.Text;
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
            ["CERT_REF"] = "CERTIFICATE-PEM",
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
        Assert.Equal("CERTIFICATE-PEM", form["cerPEM"]);
        Assert.True(form.ContainsKey("jsonB64"));

        var decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(form["jsonB64"]));
        Assert.Contains("\"issuer\":{\"rfc\":\"AAA010101AAA\"", decodedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE-KEY-PEM", decodedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("CERTIFICATE-PEM", decodedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE-KEY-PASSWORD", decodedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"certificate\"", decodedJson, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("FACTURALOPLUS_API_KEY_REFERENCE", secretResolver.RequestedKeys);
        Assert.Contains("CERT_REF", secretResolver.RequestedKeys);
        Assert.Contains("KEY_REF", secretResolver.RequestedKeys);
        Assert.DoesNotContain("PWD_REF", secretResolver.RequestedKeys);
    }

    private static FiscalStampingRequest CreateRequest()
    {
        return new FiscalStampingRequest
        {
            FiscalDocumentId = 40,
            PacEnvironment = "Sandbox",
            CfdiVersion = "4.0",
            DocumentType = "I",
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
            Subtotal = 100m,
            DiscountTotal = 0m,
            TaxTotal = 16m,
            Total = 116m,
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
                    UnitPrice = 100m,
                    DiscountAmount = 0m,
                    Subtotal = 100m,
                    TaxTotal = 16m,
                    Total = 116m,
                    SatProductServiceCode = "01010101",
                    SatUnitCode = "H87",
                    TaxObjectCode = "02",
                    VatRate = 0.16m,
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
                parts => Uri.UnescapeDataString(parts[0]),
                parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty,
                StringComparer.Ordinal);
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
