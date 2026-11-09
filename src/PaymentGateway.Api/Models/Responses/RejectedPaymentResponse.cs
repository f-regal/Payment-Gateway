namespace PaymentGateway.Api.Models.Responses;

public class RejectedPaymentResponse
{
    public RejectedPaymentResponse(IReadOnlyList<string> errors)
    {
        Errors = errors;
    }

    public PaymentStatus Status => PaymentStatus.Rejected;

    public IReadOnlyList<string> Errors { get; }
}
