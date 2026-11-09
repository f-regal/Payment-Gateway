using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc.Testing;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Tests.BankSimulator;

public class BankSimulatorTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly WebApplicationFactory<PaymentsController> _factory = new();
    private readonly HttpClient _client;

    public BankSimulatorTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private Task<HttpResponseMessage> PostPayment(string cardNumber) =>
        _client.PostAsJsonAsync("/api/Payments", new
        {
            cardNumber,
            expiryMonth = 4,
            expiryYear = DateTime.UtcNow.Year + 1,
            currency = "GBP",
            amount = 100,
            cvv = "123"
        });

    [BankSimulatorFact]
    public async Task CardEndingInOddDigit_IsAuthorized()
    {
        var response = await PostPayment("2222405343248877");
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(PaymentStatus.Authorized, payment!.Status);
    }

    [BankSimulatorFact]
    public async Task CardEndingInEvenDigit_IsDeclined()
    {
        var response = await PostPayment("2222405343248112");
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(PaymentStatus.Declined, payment!.Status);
    }

    [BankSimulatorFact]
    public async Task CardEndingInZero_ReturnsBadGateway()
    {
        var response = await PostPayment("2222405343248110");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }
}
