namespace PaymentGateway.Api.Services;

public class BankUnavailableException : Exception
{
    public BankUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
