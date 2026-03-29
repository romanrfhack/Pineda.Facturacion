using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Pineda.Facturacion.Application.Abstractions.Secrets;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Infrastructure.FacturaloPlus.FacturaloPlus;
using Pineda.Facturacion.Infrastructure.FacturaloPlus.Options;

namespace Pineda.Facturacion.UnitTests;

public class FacturaloPlusStatusQueryGatewayTests
{
    [Fact]
    public async Task QueryStatusAsync_Builds_FormUrlEncoded_Request_For_ConsultarEstadoSat()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "CodigoEstatus": "S - Comprobante obtenido satisfactoriamente.",
                  "EsCancelable": "Cancelable con aceptación",
                  "Estado": "Vigente",
                  "EstatusCancelacion": "En proceso"
                }
                """, Encoding.UTF8, "application/json")
        });

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://dev.facturaloplus.com/api/rest/servicio/")
        };
        var secretResolver = new RecordingSecretResolver(new Dictionary<string, string?>
        {
            ["FACTURALOPLUS_API_KEY_REFERENCE"] = "APIKEY-TEST"
        });

        var gateway = new FacturaloPlusStatusQueryGateway(
            client,
            Options.Create(new FacturaloPlusOptions
            {
                BaseUrl = "https://dev.facturaloplus.com/api/rest/servicio/",
                StatusQueryPath = "consultarEstadoSAT",
                ApiKeyReference = "FACTURALOPLUS_API_KEY_REFERENCE",
                ApiKeyHeaderName = "X-Api-Key"
            }),
            secretResolver);

        var result = await gateway.QueryStatusAsync(CreateRequest());

        Assert.Equal(FiscalStatusQueryGatewayOutcome.Refreshed, result.Outcome);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.EndsWith("/consultarEstadoSAT", handler.LastRequest.RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.False(handler.LastRequest.Headers.Contains("X-Api-Key"));
        Assert.Equal("application/x-www-form-urlencoded", handler.LastContentType);

        var form = ParseFormBody(handler.LastBody!);
        Assert.Equal("APIKEY-TEST", form["apikey"]);
        Assert.Equal("UUID-1", form["uuid"]);
        Assert.Equal("AAA010101AAA", form["rfcEmisor"]);
        Assert.Equal("BBB010101BBB", form["rfcReceptor"]);
        Assert.Equal("116", form["total"]);
    }

    [Fact]
    public async Task QueryStatusAsync_Maps_Sat_Response_Fields()
    {
        var gateway = CreateGateway(
            """
            {
              "CodigoEstatus": "S - Comprobante obtenido satisfactoriamente.",
              "EsCancelable": "Cancelable con aceptación",
              "Estado": "Vigente",
              "EstatusCancelacion": "En proceso"
            }
            """,
            HttpStatusCode.OK);

        var result = await gateway.QueryStatusAsync(CreateRequest());

        Assert.Equal(FiscalStatusQueryGatewayOutcome.Refreshed, result.Outcome);
        Assert.Equal("S", result.ProviderCode);
        Assert.Equal("Vigente", result.ExternalStatus);
        Assert.Equal("Cancelable con aceptación", result.Cancelability);
        Assert.Equal("En proceso", result.CancellationStatus);
        Assert.Contains("CodigoEstatus=S - Comprobante obtenido satisfactoriamente.", result.ProviderMessage);
        Assert.Contains("Estado=Vigente", result.ProviderMessage);
        Assert.Contains("EsCancelable=Cancelable con aceptación", result.ProviderMessage);
        Assert.Contains("EstatusCancelacion=En proceso", result.ProviderMessage);
        Assert.Equal("consultarEstadoSAT", result.ProviderOperation);
    }

    [Fact]
    public async Task QueryStatusAsync_Returns_ValidationFailed_When_ApiKey_Cannot_Be_Resolved()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        });
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://dev.facturaloplus.com/api/rest/servicio/")
        };
        var gateway = new FacturaloPlusStatusQueryGateway(
            client,
            Options.Create(new FacturaloPlusOptions
            {
                BaseUrl = "https://dev.facturaloplus.com/api/rest/servicio/",
                StatusQueryPath = "consultarEstadoSAT",
                ApiKeyReference = "FACTURALOPLUS_API_KEY_REFERENCE"
            }),
            new RecordingSecretResolver(new Dictionary<string, string?>()));

        var result = await gateway.QueryStatusAsync(CreateRequest());

        Assert.Equal(FiscalStatusQueryGatewayOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("API key", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryStatusAsync_Returns_Unavailable_For_Server_Error()
    {
        var gateway = CreateGateway(
            """
            {
              "CodigoEstatus": "500",
              "message": "Provider unavailable"
            }
            """,
            HttpStatusCode.InternalServerError);

        var result = await gateway.QueryStatusAsync(CreateRequest());

        Assert.Equal(FiscalStatusQueryGatewayOutcome.Unavailable, result.Outcome);
        Assert.Equal("HTTP_500", result.ErrorCode);
    }

    private static FacturaloPlusStatusQueryGateway CreateGateway(string responseJson, HttpStatusCode statusCode)
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
            ["FACTURALOPLUS_API_KEY_REFERENCE"] = "APIKEY-TEST"
        });

        return new FacturaloPlusStatusQueryGateway(
            client,
            Options.Create(new FacturaloPlusOptions
            {
                BaseUrl = "https://dev.facturaloplus.com/api/rest/servicio/",
                StatusQueryPath = "consultarEstadoSAT",
                ApiKeyReference = "FACTURALOPLUS_API_KEY_REFERENCE",
                ApiKeyHeaderName = "X-Api-Key"
            }),
            secretResolver);
    }

    private static FiscalStatusQueryRequest CreateRequest()
    {
        return new FiscalStatusQueryRequest
        {
            FiscalDocumentId = 50,
            Uuid = "UUID-1",
            IssuerRfc = "AAA010101AAA",
            ReceiverRfc = "BBB010101BBB",
            Total = 116m
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

        public Task<string?> ResolveAsync(string referenceKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_values.TryGetValue(referenceKey, out var value) ? value : null);
        }
    }
}
