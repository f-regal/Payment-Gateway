using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Services;

public class PaymentService
{
    private readonly PaymentRequestValidator _validator;
    private readonly IBankClient _bankClient;
    private readonly IPaymentsRepository _paymentsRepository;

    public PaymentService(
        PaymentRequestValidator validator,
        IBankClient bankClient,
        IPaymentsRepository paymentsRepository)
    {
        _validator = validator;
        _bankClient = bankClient;
        _paymentsRepository = paymentsRepository;
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PostPaymentRequest request)
    {
        var errors = _validator.Validate(request);

        if (errors.Count > 0)
        {
            return PaymentResult.Rejected(errors);
        }

        var cardNumber = request.CardNumber!;
        var expiryMonth = request.ExpiryMonth!.Value;
        var expiryYear = request.ExpiryYear!.Value;
        var currency = request.Currency!;
        var amount = request.Amount!.Value;
        var cvv = request.Cvv!;

        var bankRequest = new BankPaymentRequest(
            CardNumber: cardNumber,
            ExpiryDate: BankPaymentRequest.FormatExpiryDate(expiryMonth, expiryYear),
            Currency: currency,
            Amount: amount,
            Cvv: cvv);

        var bankResponse = await _bankClient.ProcessPaymentAsync(bankRequest);

        var payment = new Payment(
            Id: Guid.NewGuid(),
            Status: bankResponse.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            CardNumberLastFour: cardNumber[^4..],
            ExpiryMonth: expiryMonth,
            ExpiryYear: expiryYear,
            Currency: currency,
            Amount: amount);

        _paymentsRepository.Add(payment);

        return PaymentResult.Processed(payment);
    }
}
