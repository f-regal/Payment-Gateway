using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests.Fakes;

public sealed class FakeBankClient : IBankClient
{
    public BankPaymentResponse Response { get; set; } = new(Authorized: true, AuthorizationCode: "auth-code");

    public Exception? Exception { get; set; }

    public List<BankPaymentRequest> Requests { get; } = new();

    public Task<BankPaymentResponse> ProcessPaymentAsync(BankPaymentRequest request)
    {
        Requests.Add(request);

        if (Exception is not null)
        {
            throw Exception;
        }

        return Task.FromResult(Response);
    }
}
