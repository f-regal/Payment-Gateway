using PaymentGateway.Api.Models.Bank;

namespace PaymentGateway.Api.Services;

public interface IBankClient
{
    Task<BankPaymentResponse> ProcessPaymentAsync(BankPaymentRequest request, CancellationToken cancellationToken = default);
}
