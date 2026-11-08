namespace PaymentGateway.Api.Exceptions;

public class BankUnavailableException : Exception
{
    public BankUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
