using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public class PaymentResult
{
    private PaymentResult(PostPaymentResponse? payment, IReadOnlyList<string> errors)
    {
        Payment = payment;
        Errors = errors;
    }

    public PostPaymentResponse? Payment { get; }

    public IReadOnlyList<string> Errors { get; }

    public bool IsRejected => Errors.Count > 0;

    public static PaymentResult Processed(PostPaymentResponse payment) => new(payment, Array.Empty<string>());

    public static PaymentResult Rejected(IReadOnlyList<string> errors) => new(null, errors);
}
