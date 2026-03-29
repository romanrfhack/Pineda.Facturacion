using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Pineda.Facturacion.Application.Abstractions.Secrets;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Infrastructure.FacturaloPlus.FacturaloPlus;
using Pineda.Facturacion.Infrastructure.FacturaloPlus.Options;

namespace Pineda.Facturacion.UnitTests;

public class FacturaloPlusCancellationGatewayTests
{
    [Fact]
    public async Task CancelAsync_Builds_FormUrlEncoded_Request_For_Cancelar2()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "codigo": "201",
                  "mensaje": "Cancelado",
                  "trackingId": "TRACK-CANCEL-1"
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
            ["CERT_REF"] = """
                -----BEGIN CERTIFICATE-----
                CERT-BASE64-LINE-1
                CERT-BASE64-LINE-2
                -----END CERTIFICATE-----
                """,
            ["KEY_REF"] = """
                -----BEGIN PRIVATE KEY-----
                KEY-BASE64-LINE-1
                KEY-BASE64-LINE-2
                -----END PRIVATE KEY-----
                """,
            ["PWD_REF"] = "PRIVATE-KEY-PASSWORD"
        });

        var gateway = new FacturaloPlusCancellationGateway(
            client,
            Options.Create(new FacturaloPlusOptions
            {
                BaseUrl = "https://dev.facturaloplus.com/api/rest/servicio/",
                CancelPath = "cancelar2",
                ApiKeyReference = "FACTURALOPLUS_API_KEY_REFERENCE",
                ApiKeyHeaderName = "X-Api-Key"
            }),
            secretResolver);

        var result = await gateway.CancelAsync(CreateRequest());

        Assert.Equal(FiscalCancellationGatewayOutcome.Cancelled, result.Outcome);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.EndsWith("/cancelar2", handler.LastRequest.RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.False(handler.LastRequest.Headers.Contains("X-Api-Key"));
        Assert.Equal("application/x-www-form-urlencoded", handler.LastContentType);

        var form = ParseFormBody(handler.LastBody!);
        Assert.Equal("APIKEY-TEST", form["apikey"]);
        Assert.Equal("KEY-BASE64-LINE-1KEY-BASE64-LINE-2", form["keyCSD"]);
        Assert.Equal("CERT-BASE64-LINE-1CERT-BASE64-LINE-2", form["cerCSD"]);
        Assert.Equal("PRIVATE-KEY-PASSWORD", form["passCSD"]);
        Assert.Equal("UUID-1", form["uuid"]);
        Assert.Equal("AAA010101AAA", form["rfcEmisor"]);
        Assert.Equal("BBB010101BBB", form["rfcReceptor"]);
        Assert.Equal("116", form["total"]);
        Assert.Equal("01", form["motivo"]);
        Assert.Equal("REPL-UUID", form["folioSustitucion"]);

        Assert.Contains("FACTURALOPLUS_API_KEY_REFERENCE", secretResolver.RequestedKeys);
        Assert.Contains("CERT_REF", secretResolver.RequestedKeys);
        Assert.Contains("KEY_REF", secretResolver.RequestedKeys);
        Assert.Contains("PWD_REF", secretResolver.RequestedKeys);
    }

    [Fact]
    public async Task CancelAsync_Treats_Code201_As_Cancelled()
    {
        var gateway = CreateGateway(
            """
            {
              "codigo": "201",
              "mensaje": "Cancelado"
            }
            """,
            HttpStatusCode.OK);

        var result = await gateway.CancelAsync(CreateRequest());

        Assert.Equal(FiscalCancellationGatewayOutcome.Cancelled, result.Outcome);
        Assert.Equal("201", result.ProviderCode);
        Assert.Equal("cancelar2", result.ProviderOperation);
    }

    [Fact]
    public async Task CancelAsync_Treats_Code202_As_Cancelled()
    {
        var gateway = CreateGateway(
            """
            {
              "codigo": "202",
              "mensaje": "Previamente cancelado"
            }
            """,
            HttpStatusCode.OK);

        var result = await gateway.CancelAsync(CreateRequest());

        Assert.Equal(FiscalCancellationGatewayOutcome.Cancelled, result.Outcome);
        Assert.Equal("202", result.ProviderCode);
    }

    [Fact]
    public async Task CancelAsync_Treats_CodigoEstatus201_As_Cancelled()
    {
        var gateway = CreateGateway(
            """
            {
              "CodigoEstatus": "201",
              "Mensaje": "Cancelado",
              "trackingId": "TRACK-CANCEL-201"
            }
            """,
            HttpStatusCode.OK);

        var result = await gateway.CancelAsync(CreateRequest());

        Assert.Equal(FiscalCancellationGatewayOutcome.Cancelled, result.Outcome);
        Assert.Equal("201", result.ProviderCode);
        Assert.Equal("Cancelado", result.ProviderMessage);
        Assert.Contains("TRACK-CANCEL-201", result.SupportMessage);
        Assert.Contains("\"requestSummary\":", result.RawResponseSummaryJson);
    }

    [Fact]
    public async Task CancelAsync_Treats_NonSuccess_Code_As_Rejected()
    {
        var gateway = CreateGateway(
            """
            {
              "codigo": "203",
              "mensaje": "Solicitud rechazada"
            }
            """,
            HttpStatusCode.BadRequest);

        var result = await gateway.CancelAsync(CreateRequest());

        Assert.Equal(FiscalCancellationGatewayOutcome.Rejected, result.Outcome);
        Assert.Equal("203", result.ProviderCode);
    }

    [Fact]
    public async Task CancelAsync_Returns_ValidationFailed_When_Secrets_Cannot_Be_Resolved()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        });
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://dev.facturaloplus.com/api/rest/servicio/")
        };
        var secretResolver = new RecordingSecretResolver(new Dictionary<string, string?>
        {
            ["FACTURALOPLUS_API_KEY_REFERENCE"] = "APIKEY-TEST",
            ["KEY_REF"] = "KEY-BASE64",
            ["PWD_REF"] = "PRIVATE-KEY-PASSWORD"
        });

        var gateway = new FacturaloPlusCancellationGateway(
            client,
            Options.Create(new FacturaloPlusOptions
            {
                BaseUrl = "https://dev.facturaloplus.com/api/rest/servicio/",
                CancelPath = "cancelar2",
                ApiKeyReference = "FACTURALOPLUS_API_KEY_REFERENCE"
            }),
            secretResolver);

        var result = await gateway.CancelAsync(CreateRequest());

        Assert.Equal(FiscalCancellationGatewayOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("certificate reference", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static FacturaloPlusCancellationGateway CreateGateway(string responseJson, HttpStatusCode statusCode)
    {
        var handler = new RecordingHandler(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        });
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://dev.facturaloplus.com/api/rest/servicio/")
        };
        var secretResolver = new RecordingSecretResolver(new Dictionary<string, string?>
        {
            ["FACTURALOPLUS_API_KEY_REFERENCE"] = "APIKEY-TEST",
            ["CERT_REF"] = "CERT-BASE64",
            ["KEY_REF"] = "KEY-BASE64",
            ["PWD_REF"] = "PRIVATE-KEY-PASSWORD"
        });

        return new FacturaloPlusCancellationGateway(
            client,
            Options.Create(new FacturaloPlusOptions
            {
                BaseUrl = "https://dev.facturaloplus.com/api/rest/servicio/",
                CancelPath = "cancelar2",
                ApiKeyReference = "FACTURALOPLUS_API_KEY_REFERENCE",
                ApiKeyHeaderName = "X-Api-Key"
            }),
            secretResolver);
    }

    private static FiscalCancellationRequest CreateRequest()
    {
        return new FiscalCancellationRequest
        {
            FiscalDocumentId = 50,
            CertificateReference = "CERT_REF",
            PrivateKeyReference = "KEY_REF",
            PrivateKeyPasswordReference = "PWD_REF",
            Uuid = "UUID-1",
            IssuerRfc = "AAA010101AAA",
            ReceiverRfc = "BBB010101BBB",
            Total = 116m,
            CancellationReasonCode = "01",
            ReplacementUuid = "REPL-UUID"
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
