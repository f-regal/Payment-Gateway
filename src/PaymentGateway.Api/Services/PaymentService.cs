using PaymentGateway.Api.Exceptions;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Observability;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Services;

public class PaymentService
{
    private readonly PaymentRequestValidator _validator;
    private readonly IBankClient _bankClient;
    private readonly IPaymentsRepository _paymentsRepository;
    private readonly PaymentMetrics _metrics;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        PaymentRequestValidator validator,
        IBankClient bankClient,
        IPaymentsRepository paymentsRepository,
        PaymentMetrics metrics,
        ILogger<PaymentService> logger)
    {
        _validator = validator;
        _bankClient = bankClient;
        _paymentsRepository = paymentsRepository;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PostPaymentRequest request)
    {
        var errors = _validator.Validate(request);

        if (errors.Count > 0)
        {
            _logger.LogInformation("Payment rejected with {ErrorCount} validation errors", errors.Count);
            _metrics.RecordOutcome("rejected");

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

        BankPaymentResponse bankResponse;

        try
        {
            bankResponse = await _bankClient.ProcessPaymentAsync(bankRequest);
        }
        catch (BankUnavailableException ex)
        {
            _logger.LogWarning(ex, "Payment not processed because the acquiring bank is unavailable");
            _metrics.RecordOutcome("bank_unavailable");

            throw;
        }

        var payment = new Payment(
            Id: Guid.NewGuid(),
            Status: bankResponse.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            CardNumberLastFour: cardNumber[^4..],
            ExpiryMonth: expiryMonth,
            ExpiryYear: expiryYear,
            Currency: currency,
            Amount: amount);

        _paymentsRepository.Add(payment);

        _logger.LogInformation("Payment {PaymentId} processed with status {Status}", payment.Id, payment.Status);
        _metrics.RecordOutcome(payment.Status.ToString().ToLowerInvariant());

        return PaymentResult.Processed(payment);
    }
}
