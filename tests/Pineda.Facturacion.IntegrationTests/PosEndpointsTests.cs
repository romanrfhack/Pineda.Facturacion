using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Pineda.Facturacion.Api.Endpoints;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;

namespace Pineda.Facturacion.IntegrationTests;

public class PosEndpointsTests
{
    [Theory]
    [InlineData("search")]
    [InlineData("credit-status")]
    [InlineData("credit-check")]
    public async Task PosEndpoints_Return401_WhenJwtIsMissing(string endpointKind)
    {
        await using var factory = new MvpApiFactory();
        var client = factory.CreateClient();
        var receiverId = await SeedReceiverAsync(factory, "POSAUTH001", "POS Auth Receiver", creditEnabled: true, approvedCreditLimitAmount: 10000m);

        var response = await SendValidRequestAsync(client, endpointKind, receiverId, "POSAUTH001");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("search")]
    [InlineData("credit-status")]
    [InlineData("credit-check")]
    public async Task PosEndpoints_Return403_WhenJwtLacksPosPolicy(string endpointKind)
    {
        await using var factory = new MvpApiFactory();
        using var client = CreateClientWithJwt(factory);
        var receiverId = await SeedReceiverAsync(factory, "POSAUTH002", "POS No Access Receiver", creditEnabled: true, approvedCreditLimitAmount: 10000m);

        var response = await SendValidRequestAsync(client, endpointKind, receiverId, "POSAUTH002");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("scope", "search")]
    [InlineData("scope", "credit-status")]
    [InlineData("scope", "credit-check")]
    [InlineData("permission", "search")]
    [InlineData("permission", "credit-status")]
    [InlineData("permission", "credit-check")]
    public async Task PosEndpoints_Return200_WhenJwtIncludesPosReadClaim(string claimType, string endpointKind)
    {
        await using var factory = new MvpApiFactory();
        using var client = CreateClientWithJwt(factory, new Claim(claimType, AuthorizationPolicyNames.PosCreditReadPermission));
        var receiverId = await SeedReceiverAsync(factory, "POSAUTH003", "POS Allowed Receiver", creditEnabled: true, approvedCreditLimitAmount: 10000m);

        var response = await SendValidRequestAsync(client, endpointKind, receiverId, "POSAUTH003");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SearchReceivers_ReturnsBadRequest_WhenTermIsTooShort()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/pos/receivers/search?term=ab");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PosEndpoints.PosValidationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("TERM_TOO_SHORT", body!.ErrorCode);
    }

    [Fact]
    public async Task SearchReceivers_DoesNotExposeCreditAmounts()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        await SeedReceiverAsync(factory, "POS010101A05", "POS RFC Search Receiver", creditEnabled: true, approvedCreditLimitAmount: 8000m);

        var response = await client.GetAsync("/api/pos/receivers/search?term=POS010101A05");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var match = Assert.Single(body.RootElement.EnumerateArray());
        Assert.Equal("POS010101A05", match.GetProperty("rfc").GetString());
        Assert.False(match.TryGetProperty("creditEnabled", out _));
        Assert.False(match.TryGetProperty("approvedCreditLimitAmount", out _));
        Assert.False(match.TryGetProperty("pendingBalanceTotal", out _));
        Assert.False(match.TryGetProperty("availableCreditAmount", out _));
    }

    [Fact]
    public async Task SearchReceivers_DoesNotReturnInactiveReceivers()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        await SeedReceiverAsync(factory, "POS010101A06", "Receiver Shared Prefix", creditEnabled: true, approvedCreditLimitAmount: 5000m, isActive: false);
        var activeReceiverId = await SeedReceiverAsync(factory, "POS010101A07", "Receiver Shared Prefix Active", creditEnabled: true, approvedCreditLimitAmount: 6000m);

        var response = await client.GetAsync("/api/pos/receivers/search?term=Receiver Shared");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PosEndpoints.PosReceiverSearchResponse>>();
        Assert.NotNull(body);
        var match = Assert.Single(body!);
        Assert.Equal(activeReceiverId, match.FiscalReceiverId);
        Assert.Equal("POS010101A07", match.Rfc);
    }

    [Fact]
    public async Task SearchReceivers_FindsActiveReceiverByRfc()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var receiverId = await SeedReceiverAsync(factory, "POS010101A08", "POS RFC Search Receiver", creditEnabled: true, approvedCreditLimitAmount: 8000m);

        var response = await client.GetAsync("/api/pos/receivers/search?term=POS010101A08");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PosEndpoints.PosReceiverSearchResponse>>();
        Assert.NotNull(body);
        var match = Assert.Single(body!);
        Assert.Equal(receiverId, match.FiscalReceiverId);
        Assert.Equal("POS010101A08", match.Rfc);
        Assert.Equal("POS RFC Search Receiver", match.LegalName);
    }

    [Fact]
    public async Task SearchReceivers_FindsActiveReceiverByLegalName()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var receiverId = await SeedReceiverAsync(factory, "POS010101A09", "Receiver Alpha POS", creditEnabled: true, approvedCreditLimitAmount: 6000m);

        var response = await client.GetAsync("/api/pos/receivers/search?term=Receiver Alpha");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PosEndpoints.PosReceiverSearchResponse>>();
        Assert.NotNull(body);
        var match = Assert.Single(body!);
        Assert.Equal(receiverId, match.FiscalReceiverId);
        Assert.Equal("Receiver Alpha POS", match.LegalName);
    }

    [Fact]
    public async Task CreditStatus_Returns404_WhenReceiverDoesNotExist()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/pos/receivers/999999/credit-status");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreditStatus_Returns404_WhenReceiverIsInactive()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var receiverId = await SeedReceiverAsync(factory, "POS010101A10", "Inactive Credit Status Receiver", creditEnabled: true, approvedCreditLimitAmount: 9000m, isActive: false);

        var response = await client.GetAsync($"/api/pos/receivers/{receiverId}/credit-status");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreditStatus_ReportsPendingAndOverdueBalances_WithoutBlockingByOverdueOnly()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var receiverId = await SeedReceiverAsync(factory, "POS010101A11", "POS Pending Receiver", creditEnabled: true, approvedCreditLimitAmount: 10000m);
        await SeedInvoiceAsync(factory, receiverId, outstandingBalance: 2500m, dueAtUtc: DateTime.UtcNow.Date.AddDays(-2));
        await SeedInvoiceAsync(factory, receiverId, outstandingBalance: 1000m, dueAtUtc: DateTime.UtcNow.Date.AddDays(4));

        var response = await client.GetAsync($"/api/pos/receivers/{receiverId}/credit-status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PosEndpoints.PosReceiverCreditStatusResponse>();
        Assert.NotNull(body);
        Assert.Equal(3500m, body!.PendingBalanceTotal);
        Assert.Equal(2500m, body.OverdueBalanceTotal);
        Assert.Equal(1000m, body.CurrentBalanceTotal);
        Assert.Equal(6500m, body.AvailableCreditAmount);
        Assert.Equal(2, body.OpenInvoicesCount);
        Assert.Equal(1, body.OverdueInvoicesCount);
        Assert.True(body.CanSellOnCredit);
        Assert.Null(body.BlockReason);
    }

    [Fact]
    public async Task CreditCheck_ReturnsBlocked_WhenNoApprovedCreditExists()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var receiverId = await SeedReceiverAsync(factory, "POS010101A12", "POS No Approved Credit Receiver", creditEnabled: true, approvedCreditLimitAmount: 0m);

        var response = await client.PostAsJsonAsync($"/api/pos/receivers/{receiverId}/credit-check", new PosEndpoints.PosReceiverCreditCheckRequest
        {
            SaleAmount = 3000m,
            CurrencyCode = "MXN"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PosEndpoints.PosReceiverCreditCheckResponse>();
        Assert.NotNull(body);
        Assert.False(body!.Approved);
        Assert.Equal("NO_APPROVED_CREDIT", body.BlockReason);
    }

    [Fact]
    public async Task CreditCheck_ReturnsBlocked_WhenCreditIsDisabled()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var receiverId = await SeedReceiverAsync(factory, "POS010101A13", "POS Disabled Receiver", creditEnabled: false, approvedCreditLimitAmount: 10000m);

        var response = await client.PostAsJsonAsync($"/api/pos/receivers/{receiverId}/credit-check", new PosEndpoints.PosReceiverCreditCheckRequest
        {
            SaleAmount = 3000m,
            CurrencyCode = "MXN"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PosEndpoints.PosReceiverCreditCheckResponse>();
        Assert.NotNull(body);
        Assert.False(body!.Approved);
        Assert.Equal("CREDIT_DISABLED", body.BlockReason);
    }

    [Fact]
    public async Task CreditCheck_ReturnsApproved_WhenReceiverHasSufficientCredit()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var receiverId = await SeedReceiverAsync(factory, "POS010101A14", "POS Sufficient Receiver", creditEnabled: true, approvedCreditLimitAmount: 9000m);
        await SeedInvoiceAsync(factory, receiverId, outstandingBalance: 3792m, dueAtUtc: DateTime.UtcNow.Date.AddDays(5));

        var response = await client.PostAsJsonAsync($"/api/pos/receivers/{receiverId}/credit-check", new PosEndpoints.PosReceiverCreditCheckRequest
        {
            SaleAmount = 3000m,
            CurrencyCode = "MXN"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PosEndpoints.PosReceiverCreditCheckResponse>();
        Assert.NotNull(body);
        Assert.True(body!.Approved);
        Assert.Equal(5208m, body.AvailableCreditAmount);
        Assert.Equal(2208m, body.RemainingCreditAmount);
        Assert.Null(body.BlockReason);
    }

    [Fact]
    public async Task CreditCheck_ReturnsBlocked_WhenReceiverHasInsufficientCredit()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var receiverId = await SeedReceiverAsync(factory, "POS010101A15", "POS Insufficient Receiver", creditEnabled: true, approvedCreditLimitAmount: 4000m);
        await SeedInvoiceAsync(factory, receiverId, outstandingBalance: 1500m, dueAtUtc: DateTime.UtcNow.Date.AddDays(3));

        var response = await client.PostAsJsonAsync($"/api/pos/receivers/{receiverId}/credit-check", new PosEndpoints.PosReceiverCreditCheckRequest
        {
            SaleAmount = 3000m,
            CurrencyCode = "MXN"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PosEndpoints.PosReceiverCreditCheckResponse>();
        Assert.NotNull(body);
        Assert.False(body!.Approved);
        Assert.Equal(2500m, body.AvailableCreditAmount);
        Assert.Equal(-500m, body.RemainingCreditAmount);
        Assert.Equal("INSUFFICIENT_CREDIT", body.BlockReason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("USD")]
    [InlineData("EUR")]
    public async Task CreditCheck_ReturnsBadRequest_WhenCurrencyIsUnsupported(string currencyCode)
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var receiverId = await SeedReceiverAsync(factory, "POS010101A16", "POS Unsupported Currency Receiver", creditEnabled: true, approvedCreditLimitAmount: 9000m);

        var response = await client.PostAsJsonAsync($"/api/pos/receivers/{receiverId}/credit-check", new PosEndpoints.PosReceiverCreditCheckRequest
        {
            SaleAmount = 3000m,
            CurrencyCode = currencyCode
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PosEndpoints.PosValidationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("UNSUPPORTED_CURRENCY", body!.ErrorCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreditCheck_ReturnsBadRequest_WhenSaleAmountIsNotPositive(decimal saleAmount)
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var receiverId = await SeedReceiverAsync(factory, "POS010101A17", "POS Invalid Sale Amount Receiver", creditEnabled: true, approvedCreditLimitAmount: 9000m);

        var response = await client.PostAsJsonAsync($"/api/pos/receivers/{receiverId}/credit-check", new PosEndpoints.PosReceiverCreditCheckRequest
        {
            SaleAmount = saleAmount,
            CurrencyCode = "MXN"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PosEndpoints.PosValidationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("INVALID_SALE_AMOUNT", body!.ErrorCode);
    }

    [Fact]
    public async Task CreditCheck_Returns404_WhenReceiverIsInactive()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var receiverId = await SeedReceiverAsync(factory, "POS010101A18", "Inactive Credit Check Receiver", creditEnabled: true, approvedCreditLimitAmount: 9000m, isActive: false);

        var response = await client.PostAsJsonAsync($"/api/pos/receivers/{receiverId}/credit-check", new PosEndpoints.PosReceiverCreditCheckRequest
        {
            SaleAmount = 1000m,
            CurrencyCode = "MXN"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendValidRequestAsync(
        HttpClient client,
        string endpointKind,
        long receiverId,
        string searchTerm)
    {
        return endpointKind switch
        {
            "search" => await client.GetAsync($"/api/pos/receivers/search?term={Uri.EscapeDataString(searchTerm)}"),
            "credit-status" => await client.GetAsync($"/api/pos/receivers/{receiverId}/credit-status"),
            "credit-check" => await client.PostAsJsonAsync($"/api/pos/receivers/{receiverId}/credit-check", new PosEndpoints.PosReceiverCreditCheckRequest
            {
                SaleAmount = 1000m,
                CurrencyCode = "MXN"
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(endpointKind), endpointKind, "Unknown POS endpoint kind.")
        };
    }

    private static HttpClient CreateClientWithJwt(MvpApiFactory factory, params Claim[] extraClaims)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(factory, extraClaims));
        return client;
    }

    private static string CreateJwt(MvpApiFactory factory, params Claim[] extraClaims)
    {
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        var issuer = configuration["Auth:Jwt:Issuer"]!;
        var audience = configuration["Auth:Jwt:Audience"]!;
        var signingKey = configuration["Auth:Jwt:SigningKey"]!;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, "123"),
            new(ClaimTypes.NameIdentifier, "123"),
            new(ClaimTypes.Name, "pos-tests")
        };
        claims.AddRange(extraClaims);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<long> SeedReceiverAsync(
        MvpApiFactory factory,
        string rfc,
        string legalName,
        bool creditEnabled,
        decimal approvedCreditLimitAmount,
        int? creditDays = null,
        bool isActive = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var now = DateTime.UtcNow;

        var receiver = new FiscalReceiver
        {
            Rfc = rfc,
            LegalName = legalName,
            NormalizedLegalName = legalName.ToUpperInvariant(),
            FiscalRegimeCode = "601",
            CfdiUseCodeDefault = "G03",
            PostalCode = "64000",
            CountryCode = "MX",
            SearchAlias = legalName,
            NormalizedSearchAlias = legalName.ToUpperInvariant(),
            CreditEnabled = creditEnabled,
            ApprovedCreditLimitAmount = approvedCreditLimitAmount,
            CreditDays = creditDays,
            CreditUpdatedAtUtc = now,
            CreditUpdatedBy = "integration-test",
            IsActive = isActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.Add(receiver);
        await db.SaveChangesAsync();
        return receiver.Id;
    }

    private static async Task<long> SeedInvoiceAsync(
        MvpApiFactory factory,
        long receiverId,
        decimal outstandingBalance,
        DateTime? dueAtUtc,
        AccountsReceivableInvoiceStatus status = AccountsReceivableInvoiceStatus.Open)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var issuedAtUtc = DateTime.UtcNow.Date.AddDays(-5);

        var invoice = new AccountsReceivableInvoice
        {
            FiscalReceiverId = receiverId,
            CurrencyCode = "MXN",
            Total = outstandingBalance,
            PaidTotal = 0m,
            OutstandingBalance = outstandingBalance,
            IssuedAtUtc = issuedAtUtc,
            DueAtUtc = dueAtUtc,
            Status = status,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        db.Add(invoice);
        await db.SaveChangesAsync();
        return invoice.Id;
    }
}
