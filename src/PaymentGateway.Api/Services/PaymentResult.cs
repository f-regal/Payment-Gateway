using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Services;

public class PaymentResult
{
    private PaymentResult(Payment? payment, IReadOnlyList<string> errors)
    {
        Payment = payment;
        Errors = errors;
    }

    public Payment? Payment { get; }

    public IReadOnlyList<string> Errors { get; }

    public bool IsRejected => Errors.Count > 0;

    public static PaymentResult Processed(Payment payment) => new(payment, Array.Empty<string>());

    public static PaymentResult Rejected(IReadOnlyList<string> errors) => new(null, errors);
}
