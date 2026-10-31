using System.Net;
using System.Text;
using System.Text.Json;

using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests.Services;

public class BankClientTests
{
    private static readonly BankPaymentRequest Request = new(
        CardNumber: "2222405343248877",
        ExpiryDate: "04/2027",
        Currency: "GBP",
        Amount: 100,
        Cvv: "123");

    [Fact]
    public async Task SendsPaymentToBankInExpectedFormat()
    {
        HttpRequestMessage? sentRequest = null;
        string? sentBody = null;
        var client = CreateClient(async request =>
        {
            sentRequest = request;
            sentBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse(HttpStatusCode.OK, """{ "authorized": true, "authorization_code": "abc" }""");
        });

        await client.ProcessPaymentAsync(Request);

        Assert.NotNull(sentRequest);
        Assert.Equal(HttpMethod.Post, sentRequest!.Method);
        Assert.Equal("http://bank.test/payments", sentRequest.RequestUri!.ToString());

        using var json = JsonDocument.Parse(sentBody!);
        var root = json.RootElement;
        Assert.Equal("2222405343248877", root.GetProperty("card_number").GetString());
        Assert.Equal("04/2027", root.GetProperty("expiry_date").GetString());
        Assert.Equal("GBP", root.GetProperty("currency").GetString());
        Assert.Equal(100, root.GetProperty("amount").GetInt32());
        Assert.Equal("123", root.GetProperty("cvv").GetString());
    }

    [Fact]
    public async Task ReturnsAuthorizedResponse()
    {
        var client = CreateClient(_ => Task.FromResult(
            JsonResponse(HttpStatusCode.OK, """{ "authorized": true, "authorization_code": "0bb07405-6d44-4b50-a14f-7ae0beff13ad" }""")));

        var response = await client.ProcessPaymentAsync(Request);

        Assert.True(response.Authorized);
        Assert.Equal("0bb07405-6d44-4b50-a14f-7ae0beff13ad", response.AuthorizationCode);
    }

    [Fact]
    public async Task ReturnsDeclinedResponse()
    {
        var client = CreateClient(_ => Task.FromResult(
            JsonResponse(HttpStatusCode.OK, """{ "authorized": false, "authorization_code": "" }""")));

        var response = await client.ProcessPaymentAsync(Request);

        Assert.False(response.Authorized);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ThrowsBankUnavailable_WhenBankReturnsErrorStatus(HttpStatusCode statusCode)
    {
        var client = CreateClient(_ => Task.FromResult(JsonResponse(statusCode, "{}")));

        await Assert.ThrowsAsync<BankUnavailableException>(() => client.ProcessPaymentAsync(Request));
    }

    [Fact]
    public async Task ThrowsBankUnavailable_WhenBankCannotBeReached()
    {
        var client = CreateClient(_ => throw new HttpRequestException("Connection refused"));

        await Assert.ThrowsAsync<BankUnavailableException>(() => client.ProcessPaymentAsync(Request));
    }

    [Fact]
    public async Task ThrowsBankUnavailable_WhenBankTimesOut()
    {
        var client = CreateClient(_ => throw new TaskCanceledException("Timed out"));

        await Assert.ThrowsAsync<BankUnavailableException>(() => client.ProcessPaymentAsync(Request));
    }

    [Fact]
    public async Task ThrowsBankUnavailable_WhenBankReturnsInvalidJson()
    {
        var client = CreateClient(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK, "not json")));

        await Assert.ThrowsAsync<BankUnavailableException>(() => client.ProcessPaymentAsync(Request));
    }

    [Fact]
    public async Task DoesNotWrapCancellation_WhenCallerCancels()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var client = CreateClient(_ => throw new TaskCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ProcessPaymentAsync(Request, cts.Token));
    }

    [Theory]
    [InlineData(4, 2027, "04/2027")]
    [InlineData(12, 2030, "12/2030")]
    public void FormatExpiryDate_PadsMonthToTwoDigits(int month, int year, string expected)
    {
        Assert.Equal(expected, BankPaymentRequest.FormatExpiryDate(month, year));
    }

    private static BankClient CreateClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var httpClient = new HttpClient(new StubHandler(handler))
        {
            BaseAddress = new Uri("http://bank.test/")
        };

        return new BankClient(httpClient);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) => new(statusCode)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _handler(request);
        }
    }
}
