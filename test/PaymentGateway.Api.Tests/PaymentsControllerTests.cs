using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Exceptions;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Tests.Fakes;

namespace PaymentGateway.Api.Tests;

public class PaymentsControllerTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly int FutureYear = DateTime.UtcNow.Year + 1;

    private readonly Random _random = new();
    private readonly FakeBankClient _bankClient = new();
    private readonly PaymentsRepository _paymentsRepository = new();
    private readonly WebApplicationFactory<PaymentsController> _factory;
    private readonly HttpClient _client;

    public PaymentsControllerTests()
    {
        _factory = new WebApplicationFactory<PaymentsController>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services
                .AddSingleton<IPaymentsRepository>(_paymentsRepository)
                .AddSingleton<IBankClient>(_bankClient)));

        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static object ValidRequest() => new
    {
        cardNumber = "2222405343248877",
        expiryMonth = 4,
        expiryYear = FutureYear,
        currency = "GBP",
        amount = 100,
        cvv = "123"
    };

    [Fact]
    public async Task RetrievesAPaymentSuccessfully()
    {
        // Arrange
        var payment = new Payment(
            Id: Guid.NewGuid(),
            Status: PaymentStatus.Declined,
            CardNumberLastFour: _random.Next(1111, 9999).ToString(),
            ExpiryMonth: _random.Next(1, 13),
            ExpiryYear: _random.Next(2027, 2035),
            Currency: "GBP",
            Amount: _random.Next(1, 10000));

        _paymentsRepository.Add(payment);

        // Act
        var response = await _client.GetAsync($"/api/Payments/{payment.Id}");
        var paymentResponse = await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(payment.Id, paymentResponse!.Id);
        Assert.Equal(payment.Status, paymentResponse.Status);
        Assert.Equal(payment.CardNumberLastFour, paymentResponse.CardNumberLastFour);
        Assert.Equal(payment.ExpiryMonth, paymentResponse.ExpiryMonth);
        Assert.Equal(payment.ExpiryYear, paymentResponse.ExpiryYear);
        Assert.Equal(payment.Currency, paymentResponse.Currency);
        Assert.Equal(payment.Amount, paymentResponse.Amount);
    }

    [Fact]
    public async Task Returns404IfPaymentNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/Payments/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostPayment_ReturnsAuthorized_WhenBankAuthorizes()
    {
        _bankClient.Response = new BankPaymentResponse(Authorized: true, AuthorizationCode: "auth-code");

        var response = await _client.PostAsJsonAsync("/api/Payments", ValidRequest());
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payment);
        Assert.NotEqual(Guid.Empty, payment!.Id);
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
        Assert.Equal("8877", payment.CardNumberLastFour);
        Assert.Equal(4, payment.ExpiryMonth);
        Assert.Equal(FutureYear, payment.ExpiryYear);
        Assert.Equal("GBP", payment.Currency);
        Assert.Equal(100, payment.Amount);
    }

    [Fact]
    public async Task PostPayment_ReturnsDeclined_WhenBankDeclines()
    {
        _bankClient.Response = new BankPaymentResponse(Authorized: false, AuthorizationCode: null);

        var response = await _client.PostAsJsonAsync("/api/Payments", ValidRequest());
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(PaymentStatus.Declined, payment!.Status);
    }

    [Fact]
    public async Task PostPayment_SendsStatusAsText()
    {
        var response = await _client.PostAsJsonAsync("/api/Payments", ValidRequest());

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal("Authorized", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PostPayment_DoesNotReturnFullCardNumberOrCvv()
    {
        var response = await _client.PostAsJsonAsync("/api/Payments", ValidRequest());

        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("2222405343248877", body);
        Assert.DoesNotContain("cvv", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostPayment_SendsFullCardDetailsToBank()
    {
        await _client.PostAsJsonAsync("/api/Payments", ValidRequest());

        var sent = Assert.Single(_bankClient.Requests);
        Assert.Equal("2222405343248877", sent.CardNumber);
        Assert.Equal($"04/{FutureYear}", sent.ExpiryDate);
        Assert.Equal("GBP", sent.Currency);
        Assert.Equal(100, sent.Amount);
        Assert.Equal("123", sent.Cvv);
    }

    [Fact]
    public async Task PostPayment_CanBeRetrievedWithGet()
    {
        var postResponse = await _client.PostAsJsonAsync("/api/Payments", ValidRequest());
        var created = await postResponse.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        var getResponse = await _client.GetAsync($"/api/Payments/{created!.Id}");
        var retrieved = await getResponse.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(retrieved);
        Assert.Equal(created.Id, retrieved!.Id);
        Assert.Equal(created.Status, retrieved.Status);
        Assert.Equal(created.CardNumberLastFour, retrieved.CardNumberLastFour);
        Assert.Equal(created.ExpiryMonth, retrieved.ExpiryMonth);
        Assert.Equal(created.ExpiryYear, retrieved.ExpiryYear);
        Assert.Equal(created.Currency, retrieved.Currency);
        Assert.Equal(created.Amount, retrieved.Amount);
    }

    [Fact]
    public async Task PostPayment_ReturnsRejected_WithoutCallingBank_WhenRequestIsInvalid()
    {
        var request = new
        {
            cardNumber = "1234",
            expiryMonth = 13,
            expiryYear = FutureYear,
            currency = "JPY",
            amount = 100,
            cvv = "12"
        };

        var response = await _client.PostAsJsonAsync("/api/Payments", request);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Rejected", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(4, body.RootElement.GetProperty("errors").GetArrayLength());
        Assert.Empty(_bankClient.Requests);
    }

    [Fact]
    public async Task PostPayment_Returns400_WithoutCallingBank_WhenBodyIsNotValidJson()
    {
        var content = new StringContent("{ not json", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/Payments", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_bankClient.Requests);
    }

    [Fact]
    public async Task PostPayment_Returns502_WhenBankIsUnavailable()
    {
        _bankClient.Exception = new BankUnavailableException("Bank down");

        var response = await _client.PostAsJsonAsync("/api/Payments", ValidRequest());

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("The acquiring bank is unavailable.", body.RootElement.GetProperty("title").GetString());
    }
}
